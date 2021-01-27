namespace GtrackWeb.Models;

/// <summary>A row from <c>New_Buyer</c> as shown in the Buyer grid.</summary>
public sealed record BuyerRow(int BuyerId, string BuyerName, string MainBuyerName);
