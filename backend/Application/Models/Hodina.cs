namespace Application.Models;

public class Hodina
{
    public int Id { get; set; }
    public string Predmet { get; set; }
    public Den Den { get; set; }
    public TimeOnly Zaciatok { get; set; }
    public TimeOnly Koniec { get; set; }
    public string Ucitel { get; set; }
    
    public int MiestnostId { get; set; }
    public Miestnost Miestnost { get; set; }
}