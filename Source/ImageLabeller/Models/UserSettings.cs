using System.Collections.Generic;

namespace ImageLabeller.Models
{
    public class UserSettings
    {
        public string LastActiveView { get; set; } = "Sort";
        public string SortSourceFolder { get; set; } = string.Empty;
        public string SortDestinationFolder { get; set; } = string.Empty;
        public string LabelSourceFolder { get; set; } = string.Empty;
        public string LabeledImageDestination { get; set; } = string.Empty;
        public string LabelFileDestination { get; set; } = string.Empty;
        public int LabelSelectedClassId { get; set; } = 0;
        public List<string> ClassNames { get; set; } = new List<string>
        {
            "15", "20", "25", "30", "35", "40", "45", "50",
            "55", "60", "65", "70", "75", "80"
        };
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 700;
        public double WindowX { get; set; } = double.NaN;
        public double WindowY { get; set; } = double.NaN;
        public string ModelPath { get; set; } = string.Empty;
        public string ModelTestImagePath { get; set; } = string.Empty;
        public string ExtractLastVideoPath { get; set; } = string.Empty;
        public string ExtractOutputFolder { get; set; } = string.Empty;
        public string ExtractClassNamePrefix { get; set; } = string.Empty;
        public int ExtractLastInFrame { get; set; } = 0;
        public int ExtractLastOutFrame { get; set; } = 0;
        public string RenameSourceFolder { get; set; } = string.Empty;
        public int RenameNextFileIndex { get; set; } = 1;
        public string RenameSelectedClassName { get; set; } = string.Empty;
        public bool RenameAll { get; set; } = false;
    }
}
