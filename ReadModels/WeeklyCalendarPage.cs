using System;
using System.Collections.Generic;
using Ops.Infra.EventStore;
using Ops.Infra.ReadModels;

namespace Ops.ReadModels
{
    public class WeeklyCalendarPageReadModel : ReadModel
    {
        public WeeklyCalendarPageReadModel() { }
        public override dynamic EventPublish(EventFromES anEvent)
        {
            var readModelCollection = Book.book;
            var data = anEvent.Data;
            switch (anEvent.EventType)
            {
                case "ManagerDayScheduleSet":
                    int locationId = (int)data["LocationId"];                    
                    string day = data["Day"];
                    DateTime dayOfTheWeek;
                    DateTime.TryParse(day, out dayOfTheWeek);
                    string stringDayOfWeek = dayOfTheWeek.DayOfWeek.ToString();
                    string EOW = data["EOW"].ToString().Split(" ")[0];
                    string shiftcode = data["ShiftCode"];
                    string shiftType = getShiftDescription(readModelCollection["ShiftStatusTable"], shiftcode);
                    int locationWeekIndex = findWeekByLocation(readModelCollection["WeeklyCalendarPage"], EOW, locationId);
                    string managerId = data["ManagerId"];
                    if(locationWeekIndex == -1)
                    {
                        LocationDailyShiftRequirementsData dailyShiftRequirements =
                            (LocationDailyShiftRequirementsData)readModelCollection["LocationDailyShiftRequirements"]
                            .Find(l => l.Id == locationId.ToString());
                        if(dailyShiftRequirements == null)
                        {
                            dailyShiftRequirements = new LocationDailyShiftRequirementsData()
                            {
                                Monday = new List<string>(),
                                Tuesday = new List<string>(),
                                Wednesday = new List<string>(),
                                Thursday = new List<string>(),
                                Friday = new List<string>(),
                                Saturday = new List<string>(),
                                Sunday = new List<string>(),
                            };
                        }
                        WeeklyCalendarData weeklyCalendar = new WeeklyCalendarData
                        {
                            Approved = false,
                            EOW = EOW,
                            LocationId = locationId,
                            Days = new List<WeeklyCalendarDay>(),
                            Id = Guid.NewGuid().ToString(),
                            MissingShifts = dailyShiftRequirements
                        };
                        WeeklyCalendarDay calendarDay = new WeeklyCalendarDay
                        {
                            Date = data["Day"],
                            Shifts = new List<Shift>()
                        };
                        Shift shift = new Shift
                        {
                            ShiftType = shiftType,
                            ManagerName = getName(managerId),
                            ManagerId = managerId,
                            ShiftCode = shiftcode
                        };
                        weeklyCalendar = removeShiftFromRequirements(weeklyCalendar, stringDayOfWeek, shiftcode);
                        calendarDay.Shifts.Add(shift);
                        weeklyCalendar.Days.Add(calendarDay);
                        weeklyCalendar.Days.Sort();
                        readModelCollection["WeeklyCalendarPage"].Add(weeklyCalendar);
                    }
                    else
                    {
                        WeeklyCalendarData weeklyCalendar = (WeeklyCalendarData)readModelCollection["WeeklyCalendarPage"][locationWeekIndex];
                        int dayIndex = weeklyCalendar.Days.FindIndex(d => d.Date == day);
                        if(dayIndex == -1)
                        {
                            WeeklyCalendarDay newDay = new WeeklyCalendarDay
                            {
                                Date = data["Day"],
                                Shifts = new List<Shift>()
                            };
                            Shift shift = new Shift
                            {
                                ShiftType = shiftType,
                                ManagerName = getName(managerId),
                                ManagerId = managerId,
                                ShiftCode = shiftcode
                            };
                            weeklyCalendar = removeShiftFromRequirements(weeklyCalendar, stringDayOfWeek, shiftcode);
                            newDay.Shifts.Add(shift);
                            weeklyCalendar.Days.Add(newDay);
                        }
                        else
                        {
                            WeeklyCalendarDay calendarDay = weeklyCalendar.Days[dayIndex];
                            string managerName = getName(managerId);
                            int shiftIndex = calendarDay.Shifts.FindIndex(s => s.ManagerName == managerName && s.ShiftType == data["shiftcode"]);
                            if(shiftIndex == -1)
                            {
                                Shift shift = new Shift
                                {
                                    ShiftType = shiftType,
                                    ManagerName = managerName,
                                    ManagerId = managerId,
                                    ShiftCode = shiftcode
                                };
                                var managershiftIndex = calendarDay.Shifts.FindIndex(s => s.ManagerName == managerName);
                                var shiftCodeShiftIndex = calendarDay.Shifts.FindIndex(s => s.ShiftType == shiftType);
                                if (shiftCodeShiftIndex == -1)
                                {
                                    calendarDay.Shifts.Add(shift);
                                }
                                weeklyCalendar = removeShiftFromRequirements(weeklyCalendar, stringDayOfWeek, shiftcode);
                            }
                            else if(shiftIndex != -1 && calendarDay.Shifts[shiftIndex].ManagerName == "Cancel Shift")
                            {
                                calendarDay.Shifts[shiftIndex].ManagerName  = getName(managerId);
                            }

                        }
                    }
                    return readModelCollection;

                case "ManagerDayScheduleChangeRequested":
                    locationId = data["LocationId"];
                    day = data["ShiftDate"];
                    EOW = data["EOW"].ToString().Split(" ")[0];
                    shiftcode = data["ShiftCode"];
                    shiftType = getShiftDescription(readModelCollection["ShiftStatusTable"], shiftcode);
                    string aManagerName = data["ManagerToName"];
                    locationWeekIndex = findWeekByLocation(readModelCollection["WeeklyCalendarPage"], EOW, locationId);
                    if(locationWeekIndex != -1)
                    {
                        WeeklyCalendarData week = (WeeklyCalendarData)readModelCollection["WeeklyCalendarPage"][locationWeekIndex];
                        foreach (var aDay in week.Days)
                        {
                            if(aDay.Date == day)
                            {
                                var shift = aDay.Shifts.Find(s => s.ShiftType == shiftType);
                                shift.ManagerName = aManagerName + " (pending)";
                            }
                        }
                    }
                    return readModelCollection;

                case "ManagerDayScheduleChanged":
                    locationId = data["LocationId"];
                    day = data["ShiftDate"];
                    EOW = data["EOW"].ToString().Split(" ")[0];
                    shiftcode = data["ShiftCode"];
                    shiftType = getShiftDescription(readModelCollection["ShiftStatusTable"], shiftcode);
                    aManagerName = data["ManagerToName"];
                    locationWeekIndex = findWeekByLocation(readModelCollection["WeeklyCalendarPage"], EOW, locationId);
                    if (locationWeekIndex != -1)
                    {
                        WeeklyCalendarData week = (WeeklyCalendarData)readModelCollection["WeeklyCalendarPage"][locationWeekIndex];
                        var theDay = week.Days.Find(d => d.Date == day);
                        if(aManagerName == "Cancel Shift")
                        {
                            int dayShiftIndex = theDay.Shifts.FindIndex(s => s.ShiftCode == shiftcode);
                            if (dayShiftIndex != -1)
                            {
                                theDay.Shifts.RemoveAt(dayShiftIndex);
                            }
                        }
                        else
                        {
                            theDay.Shifts.Find(s => s.ShiftCode == shiftcode).ManagerName = aManagerName;
                        }
                                
                    }
                    return readModelCollection;

                case "GMApprovedScheduleChange":
                    string id = data["RequestId"];
                    var requestIndex = readModelCollection["ChangeRequests"].FindIndex(r => r.Id == id);
                    ChangeRequestsTableData request = (ChangeRequestsTableData)readModelCollection["ChangeRequestsTable"][requestIndex];
                    shiftType = getShiftDescription(readModelCollection["ShiftStatusTable"], request.ShiftCode);
                    locationWeekIndex = findWeekByLocation(readModelCollection["WeeklyCalendarPage"], request.EOW, Int32.Parse(request.LocationId));
                    if (locationWeekIndex != -1)
                    {
                        WeeklyCalendarData week = (WeeklyCalendarData)readModelCollection["WeeklyCalendarPage"][locationWeekIndex];
                        foreach (var aDay in week.Days)
                        {
                            if (aDay.Date == request.ShiftDate)
                            {
                                var shift = aDay.Shifts.Find(s => s.ShiftType == shiftType);
                                shift.ManagerName = request.ToName;
                            }
                        }
                    }
                    return readModelCollection;

                case "GMApprovedSchedule":
                    EOW = data["EOW"];
                    string alocationId = data["LocationId"];
                    locationId = Convert.ToInt16(alocationId);
                    locationWeekIndex = findWeekByLocation(readModelCollection["WeeklyCalendarPage"], EOW, locationId);
                    WeeklyCalendarData thisWeek = (WeeklyCalendarData)readModelCollection["WeeklyCalendarPage"][locationWeekIndex];
                    thisWeek.Approved = true;
                    return readModelCollection;
            }
            return null;
        }

        private WeeklyCalendarData removeShiftFromRequirements(WeeklyCalendarData weeklyCalendar, string stringDayOfWeek, string shiftcode)
        {
            if(stringDayOfWeek == "Monday")
            {
                int index = weeklyCalendar.MissingShifts.Monday.FindIndex(code => code == shiftcode);
                if(index != -1)
                {
                    weeklyCalendar.MissingShifts.Monday.RemoveAt(index);
                }
            }
            if (stringDayOfWeek == "Tuesday")
            {
                int index = weeklyCalendar.MissingShifts.Tuesday.FindIndex(code => code == shiftcode);
                if (index != -1)
                {
                    weeklyCalendar.MissingShifts.Tuesday.RemoveAt(index);
                }
            }
            if (stringDayOfWeek == "Wednesday")
            {
                int index = weeklyCalendar.MissingShifts.Wednesday.FindIndex(code => code == shiftcode);
                if (index != -1)
                {
                    weeklyCalendar.MissingShifts.Wednesday.RemoveAt(index);
                }
            }
            if (stringDayOfWeek == "Thursday")
            {
                int index = weeklyCalendar.MissingShifts.Thursday.FindIndex(code => code == shiftcode);
                if (index != -1)
                {
                    weeklyCalendar.MissingShifts.Thursday.RemoveAt(index);
                }
            }
            if (stringDayOfWeek == "Friday")
            {
                int index = weeklyCalendar.MissingShifts.Friday.FindIndex(code => code == shiftcode);
                if (index != -1)
                {
                    weeklyCalendar.MissingShifts.Friday.RemoveAt(index);
                }
            }
            if (stringDayOfWeek == "Saturday")
            {
                int index = weeklyCalendar.MissingShifts.Saturday.FindIndex(code => code == shiftcode);
                if (index != -1)
                {
                    weeklyCalendar.MissingShifts.Saturday.RemoveAt(index);
                }
            }
            if (stringDayOfWeek == "Sunday")
            {
                int index = weeklyCalendar.MissingShifts.Sunday.FindIndex(code => code == shiftcode);
                if (index != -1)
                {
                    weeklyCalendar.MissingShifts.Sunday.RemoveAt(index);
                }
            }
            return weeklyCalendar;
        }

        private string getShiftDescription(List<ReadModelData> list, string shiftCode)
        {
            string name = "";
           foreach(ShiftStatusTableData data in list)
            {
                if (data.StatusId == shiftCode) name = data.Description;
            }
            return name;
        }

        private string getName(string managerId)
        {
            ManagerTableData manager =  (ManagerTableData)Book.book["ManagerTable"].Find(m => m.Id == managerId);
            return manager.FirstName + " " + manager.LastName;
        }

        private int findWeekByLocation(List<ReadModelData> list, string eOW, int locationId)
        {
            
            string scheduleId = "";
            foreach(WeeklyCalendarData week in list)
            {
                if (week.EOW == eOW && week.LocationId == locationId) scheduleId = week.Id;
            }
            int index = list.FindIndex(w => w.Id == scheduleId);
            return index;
        }
    }

    public class WeeklyCalendarData : ReadModelData
    {
        public bool Approved { get; set; }
        public string EOW { get; set; }
        public int LocationId { get; set; }
        public List<WeeklyCalendarDay> Days { get; set; }
        public LocationDailyShiftRequirementsData MissingShifts { get; set; }

    }

    public class WeeklyCalendarDay
    {
        public string Date { get; set; }
        public List<Shift> Shifts { get; set; }
    }

    public class Shift
    {
        public string ShiftCode { get; set; }
        public string ManagerId { get; set; }
        public string ShiftType { get; set; }
        public string ManagerName { get; set; }
    }
   
}
