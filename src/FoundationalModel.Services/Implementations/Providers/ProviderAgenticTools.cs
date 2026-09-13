using Anthropic.Models.Messages;
using System.Text.Json;

namespace FoundationalModel.Services.Implementations.Providers
{
    public static class ProviderAgenticTools
    {
       public static List<ToolUnion> PaymentTools =

       new List<ToolUnion>
       {
          new ToolUnion(new Tool
          {
              Name = "get_payment_status",
              Description = "Gets the current status of a payment using its payment ID.",
              InputSchema = new InputSchema
              {
                  Properties = new Dictionary<string, JsonElement>
                  {
                      ["paymentId"] = JsonSerializer.SerializeToElement(new { type = "string"})
                  },
                  Required = new[] {"paymentId"}
              }
          }),
          new ToolUnion(new Tool
          {
              Name = "get_customer_balance",
              Description = "Gets the current balance for a customer.",
              InputSchema = new InputSchema
              {
                  Properties = new Dictionary<string, JsonElement>
                  {
                      ["csutomerId"] = JsonSerializer.SerializeToElement(new {type  = "string"})
                  }, Required = new[] { "customerId"}
              }
          })

       };

    }
}
