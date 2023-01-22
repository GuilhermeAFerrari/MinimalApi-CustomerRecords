namespace MinimalApi_CustomerRecords.Models;

public class Customer
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Document { get; set; }
    public bool Active { get; set; }
}