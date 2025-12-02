namespace ImageLabeller.Models
{
    public class UserSettings
    {
        public string LastActiveView { get; set; } = "Sort";
        public string SortSourceFolder { get; set; } = string.Empty;
        public string SortDestinationFolder { get; set; } = string.Empty;
        public string LabelSourceFolder { get; set; } = string.Empty;
        public int LabelSelectedClassId { get; set; } = 0;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 700;
        public double WindowX { get; set; } = double.NaN;
        public double WindowY { get; set; } = double.NaN;
    }
}
