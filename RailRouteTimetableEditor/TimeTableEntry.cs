namespace RailRouteTimetableEditor
{
    internal class TimeTableEntry
    {
        public Station Station {  get; set; }
        public int Platform { get; set; }
        public TimeSpan Arrival { get; set; }
        public TimeSpan Departure { get; set; }

    }
}
