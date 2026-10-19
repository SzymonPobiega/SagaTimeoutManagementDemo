namespace OrderEndpoint;

public class PlaceOrder : ICommand
{
    public required string OrderId { get; set; }
}

public class PostponeBuyersRemorse : ICommand
{
    public required string OrderId { get; set; }
}

public class ExpediteBuyersRemorse : ICommand
{
    public required string OrderId { get; set; }
}

public class BuyersRemorseIsOver
{
    public required string OrderId { get; set; }
    public string? Note { get; set; }
}
