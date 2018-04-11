using System;
using System.Collections.Generic;
using Ops.Infra;
using Ops.Infra.CommandToPublishEvent;
using Ops.Infra.EventStore;
using Ops.Models.commands;
using Ops.Models.events;
using Ops.Models.Commands;
using Ops.Models.Events;

namespace Ops.Domain
{
    public class ManagerAggregate : Aggregate
    {
        string _Id;
        List<string> _statHolidaysWorked = new List<string>();

        public override Events[] Execute(Commands cmd)
        {
            if (cmd is CreateManager) { return _CreateManager((CreateManager)cmd); }
            if (cmd is ChangeManagerEmail) { return _ChangeManagerEmail((ChangeManagerEmail)cmd); }
            if (cmd is ChangeManagerRole) { return _ChangeManagerRole((ChangeManagerRole)cmd); }
            return null;
        }

        private Events[] _ChangeManagerRole(ChangeManagerRole cmd)
        {
            if (string.IsNullOrEmpty(cmd.Id)) { throw new Exception("Id is a required Field"); }
            if (string.IsNullOrEmpty(_Id)) { throw new Exception("Manager not found"); }
            ManagerRoleChanged managerRoleChanged = new ManagerRoleChanged
            {
                ManagerId = cmd.Id,
                Role = cmd.Role
            };
            return new Events[] { managerRoleChanged };
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

        private Events[] _CreateManager(CreateManager cmd)
        {
            if (!string.IsNullOrEmpty(_Id)) { throw new Exception("Manager already created"); }
            if (string.IsNullOrEmpty(cmd.Id)) { throw new Exception("Id is a required field"); }
            if (string.IsNullOrEmpty(cmd.FirstName)) { throw new Exception("FirstName is a required field"); }
            if (string.IsNullOrEmpty(cmd.LastName)) { throw new Exception("LastName is a required field"); }
            if (string.IsNullOrEmpty(cmd.EmailAddress)) { throw new Exception("Email is a required field"); }
            if (cmd.Role == -1 || cmd.Role == 0) { throw new Exception("Role is a required field"); }
            if (cmd.VacationDaysOwed == -1) cmd.VacationDaysOwed = 0;
            if (cmd.VacationRate == -1) cmd.VacationRate = 0;
            ManagerCreated created = new ManagerCreated
            {
                ManagerId = cmd.Id,
                LocationId = cmd.LocationId,
                FirstName = cmd.FirstName,
                LastName = cmd.LastName,
                Role = cmd.Role,
                VacationRate = cmd.VacationRate,
                VacationDaysOwed = cmd.VacationDaysOwed,
                EmailAddress = cmd.EmailAddress
            };
            return new Events[] { created };
        }

        public override void Hydrate(EventFromES evt)
        {
            if (evt.EventType == "ManagerCreated") { _onManagerCreated(evt); }
        }

        private void _onManagerCreated(EventFromES evt)
        {
            _Id = evt.Data["ManagerId"];
        }

    }
}
