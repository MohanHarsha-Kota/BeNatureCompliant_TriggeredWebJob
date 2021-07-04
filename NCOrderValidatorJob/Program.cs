using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs;
using NCOrderValidatorJob.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCOrderValidatorJob
{
    // To learn more about Microsoft Azure WebJobs SDK, please see https://go.microsoft.com/fwlink/?LinkID=320976
    class Program
    {
        // Please set the following connection strings in app.config for this WebJob to run:
        // AzureWebJobsDashboard and AzureWebJobsStorage

        static SecretClientOptions options = new SecretClientOptions()
        {
            Retry =
            {
                Delay= TimeSpan.FromSeconds(2),
                MaxDelay = TimeSpan.FromSeconds(16),
                MaxRetries = 5,
                Mode = RetryMode.Exponential
            }
        };

        private static SecretClient client = new SecretClient(new Uri("https://nc-poc-keyvault.vault.azure.net/"), new DefaultAzureCredential(), options);
        private static KeyVaultSecret secret = client.GetSecret("nc-cosmos-access-key");

        private static string secretValue = secret.Value;


        private static string endpointuri = "https://nc-poc-cosmosdb.documents.azure.com:443/";
        private static CosmosClient cosmosClient = new CosmosClient(endpointuri, secretValue);
        private static Database Ncdatabase = cosmosClient.GetDatabase("NCMasterDB");

        public static async Task Main()
        {
            //Fetching the Serviced/Declined Orders
            List<UpdateModel> responseData = await FetchData();
            //Checking if there are any..
            if(responseData.Count != 0)
            {
                await SetUpdatedData(responseData);
                Console.WriteLine("Runtime: " + DateTime.Now +" Number of records processed :"+ responseData.Count);
            }
            else
            {
                Console.WriteLine("Runtime: " + DateTime.Now + " Number of records processed :" + responseData.Count);
            }    
        }

        private static async Task SetUpdatedData(List<UpdateModel> updObject)
        {
            Container NCScheduleHisCon = Ncdatabase.GetContainer("ScheduleHistory");
            try
            {   //For each and every serviced/declind order, update the corresponding details in the schedule history container
                for (int i = 0; i < updObject.Count; i++)
                {
                    ItemResponse<ScheduleHistoryModel> SchObj = await NCScheduleHisCon.ReadItemAsync<ScheduleHistoryModel>(updObject[i].OrderScheduleId, new PartitionKey(updObject[i].EmailId));
                    var itemBody = SchObj.Resource;

                    if (updObject[i].Status == "Serviced" && itemBody.Status != "Completed")
                    {
                        itemBody.Status = "Completed";
                        SchObj = await NCScheduleHisCon.ReplaceItemAsync<ScheduleHistoryModel>(itemBody, itemBody.OrderScheduleId, new PartitionKey(itemBody.EmailId));
                    }
                    else if (updObject[i].Status == "Declined" && itemBody.Status != "Cancelled")
                    {
                        itemBody.Status = "Cancelled";
                        SchObj = await NCScheduleHisCon.ReplaceItemAsync<ScheduleHistoryModel>(itemBody, itemBody.OrderScheduleId, new PartitionKey(itemBody.EmailId));
                    }
                    else
                    {
                        continue;
                    }
                    
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }
        }

        private static async Task<List<UpdateModel>>FetchData()
        {
            Container NCServiceHistCon = Ncdatabase.GetContainer("ServiceHistory");
            string today_date = DateTime.Now.ToString("dd-MM-yyyy");
            string sqlQueryText = string.Format("SELECT * from c where c.serviced_date='{0}'", today_date);

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            FeedIterator<ServicedOrderModel> queryResultSetIterator = NCServiceHistCon.GetItemQueryIterator<ServicedOrderModel>(queryDefinition);

            List<UpdateModel> orderList = new List<UpdateModel>();
            while (queryResultSetIterator.HasMoreResults)
            {
                FeedResponse<ServicedOrderModel> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                foreach (ServicedOrderModel p in currentResultSet)
                {
                    UpdateModel uObj = new UpdateModel(p.OrderScheduleId,p.CustomerEmailId ,p.Status);
                    orderList.Add(uObj);
                }
            }
            return orderList;
        }
    }
}
