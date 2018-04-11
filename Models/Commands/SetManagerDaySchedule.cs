using Ops.Infra;
using Ops.ReadModels;

namespace Ops.Models.commands
{
    public class SetManagerDaySchedule : Ops.Infra.Commands
    {
        public string ManagerId { get; set; }
        public string LocationId { get; set; }
        public string ShiftCode { get; set; }
        public string EOW { get; set; }
        public string Day { get; set; }
        public string ShiftStatus { get; set; }
    }
}
