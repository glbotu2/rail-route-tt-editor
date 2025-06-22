namespace RailRouteTimetableEditor
{
    internal class Train
    {
        public string HeadCode { get; set; } = string.Empty;
        public int MaxSpeed { get; set; }
        public TrainType Classification { get; set; }
        public string Composition { get; set; } = string.Empty;
        public bool Penalty { get; set; }
        public List<TimeTableEntry> TimeTable { get; set; } = new List<TimeTableEntry>();
        public bool HasCollision { get; set; }
        public string CollidesWith { get; set; } = string.Empty;
    }
}
