using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ops.Infra;
using Ops.Infra.CommandToPublishEvent;
using Ops.Infra.EventStore;
using Ops.Models.commands;
using Ops.Models.events;
using Ops.ReadModels;

namespace Ops.Domain
{
    public class ScheduleAggregate : Aggregate
    {
        string _Id;
        bool _approved;
        public override Events[] Execute(Commands cmd)
        {
            if (cmd is SetManagerDaySchedule) { return _SetManagerDaySchedule((SetManagerDaySchedule)(cmd)); }
            if (cmd is RequestChangeManagerSchedule) { return _RequestChangeManagerSchedule((RequestChangeManagerSchedule)cmd); }
            if (cmd is GMApproveScheduleChange) { return _GMApproveChange((GMApproveScheduleChange)cmd); }
            if (cmd is GMRejectScheduleChange) { return _GMRejectScheduleChange((GMRejectScheduleChange)cmd); }
            if (cmd is PayrollApproveScheduleChange) { return _PayrollApproveScheduleChange((PayrollApproveScheduleChange)cmd); }
            if (cmd is GMApproveSchedule) { return _GMApproveSchedue((GMApproveSchedule)cmd); }
            if (cmd is GMRejectSchedule) { return _GMRejectSchedule((GMRejectSchedule)cmd); }
            return null;
        }

        private Events[] _GMRejectSchedule(GMRejectSchedule cmd)
        {
            if (string.IsNullOrEmpty(_Id)) throw new Exception("Restaurant schedule not found");
            if (string.IsNullOrEmpty(cmd.LocationId)) throw new Exception("Locaiton ID is a required string");
            if (string.IsNullOrEmpty(cmd.EOW)) throw new Exception("End of week is a required field");
            GMRejectedSchedule schedule = new GMRejectedSchedule
            {
                LocationId = cmd.LocationId,
                EOW = cmd.EOW
            };
            return new Events[] { schedule };
        }

        private Events[] _PayrollApproveScheduleChange(PayrollApproveScheduleChange cmd)
        {
            if (string.IsNullOrEmpty(cmd.Id)) { throw new Exception("ManagerId is a required field"); }
            if (string.IsNullOrEmpty(_Id)) { throw new Exception("Manager not found"); }
            if (cmd.Request == null) { throw new Exception("Request isn't there"); }
            PayRollApprovedScheduleChange payroll = new PayRollApprovedScheduleChange
            {
                Id = cmd.Id,
                RequestId = cmd.Request.Id
            };
            return new Events[] { payroll };
        }

        private Events[] _GMRejectScheduleChange(GMRejectScheduleChange cmd)
        {
            if (string.IsNullOrEmpty(_Id)) { throw new Exception("Manager not found"); }
            if (string.IsNullOrEmpty(cmd.Id)) { throw new Exception("ManagerId is a required field"); }
            if (string.IsNullOrEmpty(cmd.GMId)) { throw new Exception("General Manager Id is a required field"); }
            if (string.IsNullOrEmpty(cmd.RequestId)) { throw new Exception("RequestId is a required field"); }
            // to origional shift manager
            SendSchedulingEmail.RejectedEmailBuilder(cmd.ManagerTo.ManagerName, cmd.ManagerFrom.ManagerName,
                "", cmd.GM.EmailAddress, cmd.ManagerFrom.EmailAddress, cmd.Reason);
            // to pending shift manager
            SendSchedulingEmail.RejectedEmailBuilder(cmd.ManagerFrom.ManagerName, cmd.GM.ManagerName,
                "", cmd.GM.EmailAddress, cmd.ManagerTo.EmailAddress, cmd.Reason);
            return new Events[] { new GMRejectedScheduleChange
                {
                     RequestId = cmd.RequestId,
                     Reason = cmd.Reason,
                     OrigionalManagerName = cmd.OrigionalRequest.FromName,
                     OriginalManagerId = cmd.OrigionalRequest.FromId
                }
            };
        }

        private Events[] _GMApproveChange(GMApproveScheduleChange cmd)
        {
            List<Events> events = new List<Events>();
            if (string.IsNullOrEmpty(_Id)) { throw new Exception("Manager not found"); }
            if (string.IsNullOrEmpty(cmd.Id)) { throw new Exception("ManagerId is a required field"); }
            if (string.IsNullOrEmpty(cmd.GMId)) { throw new Exception("General Manager Id is a required field"); }
            if (string.IsNullOrEmpty(cmd.Request.Id)) { throw new Exception("RequestId is a required field"); }
            int EOW = DateTime.Parse(cmd.Request.EOW).DayOfYear;
            int today = DateTime.Now.DayOfYear;
            int daysUntilEndOfWeek = EOW - today;
            ChangeRequestsTableData request = cmd.Request;
            if (daysUntilEndOfWeek <= 6)
            {
                SendSchedulingEmail.EmailBuilder(request.ToName, request.FromName, "104", request.ShiftDate,
                    "josh.ogden@whitespost.ca", "josh.ogden@whitespot.ca", request.Reason, request.Id, "payrollApproval");
                PayrollApprovalRequiredForShiftChange payrollApprovalRequired = new PayrollApprovalRequiredForShiftChange
                {
                    Id = cmd.Id,
                    RequestId = request.Id
                };
                events.Add(payrollApprovalRequired);
            }
            GMApprovedScheduleChange approval = new GMApprovedScheduleChange
            {
                ManagerId = cmd.ManagerId,
                GMId = cmd.GMId,
                RequestId = cmd.Request.Id
            };
            events.Add(approval);
            return events.ToArray();

        }

        private Events[] _RequestChangeManagerSchedule(RequestChangeManagerSchedule cmd)
        {
            if (cmd.Id == "Cancel Shift")
            {
                ManagerDayScheduleChangeRequested cancelled = new ManagerDayScheduleChangeRequested
                {
                    ManagerId = cmd.ManagerId,
                    RequestId = cmd.RequestId,
                    Reason = cmd.Reason,
                    ShiftCode = cmd.ShiftCode,
                    ShiftDate = cmd.ShiftDate,
                    EOW = cmd.EOW,
                    LocationId = cmd.LocationId
                };
                if (_approved)
                {
                    cancelled.Pending = true;
                    SendSchedulingEmail.EmailBuilder(cmd.ManagerToName, cmd.ManagerFromName, cmd.LocationId,
                        cmd.ShiftDate, cmd.ManagerEmailAddress, cmd.GMEmailAddress, cmd.Reason, cmd.RequestId, "approveChange");
                }
                else cancelled.Pending = false;
                return new Events[] { cancelled };
            }
            if (string.IsNullOrEmpty(_Id)) { throw new Exception("Manager Id is a required field"); }
            if (string.IsNullOrEmpty(cmd.Reason)) { throw new Exception("Reason is a required field"); }
            if (string.IsNullOrEmpty(cmd.EOW)) { throw new Exception("End Of Week date is a required field"); }
            if (string.IsNullOrEmpty(cmd.ShiftDate)) { throw new Exception("Shift Date is a required field"); }
            if (string.IsNullOrEmpty(cmd.ShiftCode)) { throw new Exception("Shift Code is a required field"); }
            if (string.IsNullOrEmpty(cmd.RequestId)) { throw new Exception("Request Id is a required field"); }
            if (string.IsNullOrEmpty(cmd.LocationId)) { throw new Exception("Location Id is a required field"); }
            if (string.IsNullOrEmpty(cmd.ManagerToName)) { throw new Exception("Manager To is a required field"); }
            if (string.IsNullOrEmpty(cmd.ManagerFromName)) { throw new Exception("Managerfrom is a required field"); }
            if (_approved == true)
            {
                ManagerDayScheduleChangeRequested request = new ManagerDayScheduleChangeRequested
                {
                    ManagerId = cmd.ManagerId,
                    RequestId = cmd.RequestId,
                    Reason = cmd.Reason,
                    ShiftCode = cmd.ShiftCode,
                    ShiftDate = cmd.ShiftDate,
                    EOW = cmd.EOW,
                    LocationId = cmd.LocationId,
                    ManagerFromName = cmd.ManagerFromName,
                    ManagerToName = cmd.ManagerToName,
                    ManagerEmailAddress = cmd.ManagerEmailAddress,
                    GMEmailAddress = cmd.GMEmailAddress
                };
                DateTime shiftdate = DateTime.Parse(cmd.ShiftDate);
                DateTime endOfWeek = DateTime.Parse(cmd.EOW);
                request.Pending = true;
                SendSchedulingEmail.EmailBuilder(cmd.ManagerToName, cmd.ManagerFromName, cmd.LocationId,
                    cmd.ShiftDate, cmd.ManagerEmailAddress, cmd.GMEmailAddress, cmd.Reason, cmd.RequestId, "approveChange");
                request.Pending = true;
                return new Events[] { request };
            }
            else
            {
                ManagerDayScheduleChanged change = new ManagerDayScheduleChanged
                {
                    StreamId = "ScheduleAggregate." + cmd.LocationId + "." + cmd.EOW,
                    ManagerId = cmd.ManagerId,
                    RequestId = cmd.RequestId,
                    Reason = cmd.Reason,
                    ShiftCode = cmd.ShiftCode,
                    ShiftDate = cmd.ShiftDate,
                    EOW = cmd.EOW,
                    LocationId = cmd.LocationId,
                    ManagerToName = cmd.ManagerToName,
                };
                return new Events[] { change };
            }
        }

        private Events[] _SetManagerDaySchedule(SetManagerDaySchedule cmd)
        {
            if (string.IsNullOrEmpty(cmd.Day)) { throw new Exception("Day is a required field"); }
            if (string.IsNullOrEmpty(cmd.ManagerId)) { throw new Exception("ManagerId is a required field"); }
            if (string.IsNullOrEmpty(cmd.ShiftCode)) { throw new Exception("shift code is a required field"); }
            if (cmd.LocationId == 0 || cmd.LocationId == -1) { throw new Exception("Location Id is a required field"); }
            ManagerDayScheduleSet manager = new ManagerDayScheduleSet
            {
                LocationId = cmd.LocationId,
                ManagerId = cmd.ManagerId,
                ShiftCode = cmd.ShiftCode,
                Day = cmd.Day,
                EOW = cmd.EOW
            };
            return new Events[] { manager };
        }

        private Events[] _ChangeManagerEmail(ChangeManagerEmail cmd)
        {
            if (string.IsNullOrEmpty(cmd.Id)) { throw new Exception("Id is a required Field"); }
            if (string.IsNullOrEmpty(cmd.EmailAddress)) { throw new Exception("Email address is a required feild"); }
            ManagerEmailChanged managerEmail = new ManagerEmailChanged
            {
                ManagerId = cmd.Id,
                EmailAddress = cmd.EmailAddress
            };
            return new Events[] { managerEmail };

        }

        private Events[] _GMApproveSchedue(GMApproveSchedule cmd)
        {
            if (string.IsNullOrEmpty(cmd.EOW)) { throw new Exception("End of Week Date is a required field"); }
            if (string.IsNullOrEmpty(cmd.Id)) { throw new Exception("Location Id is a required field"); }
            if (string.IsNullOrEmpty(cmd.GMId)) { throw new Exception("General Manager id is a required field"); }
            foreach (ManagerTableData manager in cmd.Managers)
            {
                SendSchedulingEmail.AcceptedEmailBuilder(cmd.EOW, cmd.RestaurantId, manager.EmailAddress);
            }
            GMApprovedSchedule schedule = new GMApprovedSchedule
            {
                GMId = cmd.GMId,
                LocationId = cmd.RestaurantId,
                EOW = cmd.EOW
            };
            return new Events[] { schedule };
        }

        public override void Hydrate(EventFromES evt)
        {
            if (evt.EventType == "GMApprovedSchedule") { _onGMApprovedSchedule(evt); }
            if (evt.EventType == "ManagerDayScheduleSet") { _onManagerDayScheduleSet(evt); }
        }

        private void _onGMApprovedSchedule(EventFromES evt)
        {
            _approved = true;
        }

        private void _onManagerDayScheduleSet(EventFromES evt)
        {
            _Id = evt.StreamId;
            _approved = false;
        }


    }

    class SendSchedulingEmail
    {
        public static void EmailBuilder(string managerToName, string managerFromName,
            string locationId, string date, string theSender, string theReceiver,
            string reason, string requestId, string page)
        {
            string sender = theSender;
            string receiver = theReceiver;
            string subject = "Manager " + managerToName + " has requested a shift change";
            string body = managerToName + " from location " +
                locationId + " has requested that their shift on " + date + "  "
                + "Be changed with " + managerFromName + "<br/> The reason that they gave was: '" +
                reason + ".'<br/> " + "Please click  <a href='http://localhost:8080/" + page + "/?Id=" + requestId + "'>here</a> to check.";
            Emailer.Email(sender, receiver, subject, body);
        }

        public static void RejectedEmailBuilder(string managerName, string gmName, string date, string theSender, string theReceiver,
            string reason)
        {
            string subject = gmName + " has refused your shift change with " + managerName + " request on " + date + ".";
            string body = gmName + " did not accept your request for a shift change on " + date + ".  </b>The reason given was " + reason;
            Emailer.Email(theReceiver, theReceiver, subject, body);
        }

        public static void AcceptedEmailBuilder(string eow, string locationId, string theReceiver)
        {
            string sender = "theHub@whitespot.ca";
            string receiver = theReceiver;
            string subject = "Schedule for the week ending on " + eow;
            string body = "The weekly schedule ending on " + eow + " at restaurant " +
                locationId + " has been set.</br>" + "Please click  <a href=http://localhost:8080/#/schedule >here</a> to check.";
            Emailer.Email(sender, receiver, subject, body);
        }
    }

    class DailyShift
    {
        string ShiftId { get; set; }
        string ShiftDate { get; set; }
        string EndOfWeek { get; set; }
    }
}
