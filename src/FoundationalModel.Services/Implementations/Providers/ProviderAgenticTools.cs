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
              Description = "Gets the current status and failure details of a specific payment. It does not provide operational guidance, customer communication advice" +
              "or internal procedures",
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
                      ["customerId"] = JsonSerializer.SerializeToElement(new {type  = "string"})
                  }, Required = new[] { "customerId"}
              }
          }),
          new ToolUnion(new Tool
          {
              Name = "search_knowledge_base",
              Description = "Searches the internal knowledge base for information relevant to a question or issue. Use this when you need documentation, policies, procedures" +
              "or technical guidance. Use this when the user asks what should be done, what guidance applies, or what should be communicated",
              InputSchema = new InputSchema 
              {
                  Properties = new Dictionary<string, JsonElement>
                  {
                      ["query"] = JsonSerializer.SerializeToElement(new {type = "string", description = "what to search for in the knowledge base"
                  })
                  }, Required = new[] {"query" }

              }
          })
       };

    }
}
