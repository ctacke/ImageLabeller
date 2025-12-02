namespace ImageLabeller.Models
{
    public class ImageClass
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#FFFFFF";

        public ImageClass(int id, string name, string color)
        {
            Id = id;
            Name = name;
            Color = color;
        }
    }
}
