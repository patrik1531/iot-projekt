namespace Application.Models;

public class Miestnost
{
    public int Id { get; set; }
    public string Nazov { get; set; }
    public string Oznacenie { get; set; }
    public string Budova { get; set; }
    public string TypMiestnosti { get; set; }
    
    public List<Hodina> Hodiny { get; set; }
}