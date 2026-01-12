namespace AutoPoster.Models;

public class DatQueue
{
    public int SortIndex { get; set; }
    public DateTime PostTime { get; set; }
    public string Message { get; set; } = "";
}
