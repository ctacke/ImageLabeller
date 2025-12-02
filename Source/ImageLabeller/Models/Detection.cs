namespace ImageLabeller.Models
{
    public class Detection
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public float X { get; set; }  // Normalized 0-1
        public float Y { get; set; }  // Normalized 0-1
        public float Width { get; set; }  // Normalized 0-1
        public float Height { get; set; }  // Normalized 0-1

        public Detection(int classId, string className, float confidence, float x, float y, float width, float height)
        {
            ClassId = classId;
            ClassName = className;
            Confidence = confidence;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
