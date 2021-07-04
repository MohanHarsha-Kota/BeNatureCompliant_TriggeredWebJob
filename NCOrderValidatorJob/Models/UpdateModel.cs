using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NCOrderValidatorJob.Models
{
    public class UpdateModel
    {
        
        public string OrderScheduleId { get; set; }

        public string Status { get; set; }

        public string EmailId { get; set; }

        public UpdateModel(string OrderScheduleId, string EmailId, string Status)
        {
            this.OrderScheduleId = OrderScheduleId;
            this.Status = Status;
            this.EmailId = EmailId;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
