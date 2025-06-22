namespace RailRouteTimetableEditor
{
    public class Station
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<int> Platforms { get; set; } = new List<int>();
    }
}
