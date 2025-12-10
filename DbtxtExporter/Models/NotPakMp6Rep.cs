namespace DbtxtExporter.Models;

public class NotPakMp6Rep
{
    public int Id { get; set; }
    public DateTime DateTime { get; set; }
    public DateTime DateTimeUpdate { get; set; }
    public int? Smena { get; set; }
    public int? Plan { get; set; }
    public int? DelayTimePrevHour { get; set; }
    public int? DelayTimeThisHour { get; set; }
}