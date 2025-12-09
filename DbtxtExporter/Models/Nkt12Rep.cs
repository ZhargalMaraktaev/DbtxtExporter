namespace DbtxtExporter.Models;

public class Nkt12Rep
{
    public int Id { get; set; }
    public DateTime DateTime { get; set; }
    public float? Diam { get; set; }
    public float? Thikness { get; set; }
    public int? Pak_Num { get; set; }
    public int? Pipe_Num { get; set; }
    public float? Weight { get; set; }
    public float? Length { get; set; }
    public string? Hardness { get; set; }
    public int? Class { get; set; }
    public byte? Marked { get; set; }
    public byte? Measured { get; set; }
    public int? PressPipeNum { get; set; }
    public float? Teo_Weight { get; set; }
    public int? Pipe_Status { get; set; }
    public int? Kar_Num { get; set; }
    public int? Smena { get; set; }
    public int? Plan { get; set; } = 20;
    public int Deleted { get; set; } = 0;
    public string? ProtDiv { get; set; }
    public int? DelayTimePrevHour { get; set; }
    public int? DelayTimeThisHour { get; set; }
    public int? Veracity { get; set; }
}