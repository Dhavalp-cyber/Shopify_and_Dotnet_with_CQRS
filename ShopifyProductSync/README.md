# ShopifyProductSync — Order Fulfillment Feature

## Overview

This project is a .NET 8 Web API that integrates with Shopify using CQRS + MediatR pattern.
This README covers the **Order Fulfillment** feature added in `feature/shopify-order-fulfillment`.

---

## 1. How to Create a Shopify Custom App

1. Go to your Shopify Admin → **Settings** → **Apps and sales channels**
2. Click **Develop apps** → **Create an app**
3. Give it a name (e.g. `DotNet_Fulfillment_App`)
4. Click **Create app**

---

## 2. Required Shopify App Scopes

In the app → **Configuration** tab → **Edit** (Admin API integration), enable:

| Scope | Purpose |
|---|---|
| `read_orders` | Fetch order details |
| `write_orders` | Update order status |
| `read_merchant_managed_fulfillment_orders` | Fetch fulfillment orders |
| `write_merchant_managed_fulfillment_orders` | Create fulfillments |
| `read_products` | Existing product functionality |
| `write_products` | Existing product functionality |
| `read_inventory` | Existing inventory functionality |
| `write_inventory` | Existing inventory functionality |
| `read_locations` | Fetch location names |

Click **Save**, then go to **Overview** tab → **Install app**.

---

## 3. Where to Get the Access Token

After installing the app:
1. Go to **API credentials** tab
2. Copy the **Admin API access token** — it is shown **only once**
3. It starts with `shpat_`

---

## 4. How to Configure appsettings.json

Update `appsettings.Development.json` (never commit this file — it is in `.gitignore`):

```json
{
  "Shopify": {
    "ShopUrl": "your-store.myshopify.com",
    "AccessToken": "shpat_your_token_here",
    "ApiSecretKey": "your_api_secret_key",
    "ApiVersion": "2026-01",
    "AllowedTrackingCarriers": [
      "Aramex", "DHL", "FedEx", "UPS", "Manual"
    ]
  }
}
```

---

## 5. How to Run the Project

```bash
dotnet run --project ShopifyProductSync/ShopifyProductSync.csproj --launch-profile http
```

Open Swagger UI at: **http://localhost:5215**

---

## 6. How to Test POST /api/orders/fulfill

In Swagger, find `POST /api/orders/fulfill` and use this request body:

```json
{
  "orderId": 123456789,
  "trackingNumber": "TRACK123456",
  "shippingCarrierName": "Aramex",
  "notifyCustomer": true
}
```

**Success response (200):**
```json
{
  "message": "Order fulfilled successfully.",
  "orderId": 123456789,
  "fulfillmentId": "987654321",
  "trackingNumber": "TRACK123456",
  "shippingCarrierName": "Aramex",
  "notifyCustomer": true
}
```

**Possible error responses:**
- `404` — Order not found
- `400` — Order already fulfilled / Invalid carrier / No open fulfillment order

---

## 7. Why Webhook is Not Required

This API uses **manual fulfillment triggering**. The flow is:

```
Your API call → Shopify fulfillment created → Shopify sends webhook (optional)
```

Webhooks are for **receiving** events from Shopify (e.g. when someone fulfills an order in Shopify Admin).
Since we are **creating** the fulfillment ourselves via API, no webhook is needed.

---

## 8. Difference Between OrderId, FulfillmentOrderId, and FulfillmentId

| Term | What it is | Example |
|---|---|---|
| `OrderId` | The Shopify order number | `123456789` |
| `FulfillmentOrderId` | A group of line items to be fulfilled together | `987654321` |
| `FulfillmentId` | The actual fulfillment record created | `111222333` |

**Flow:**
```
OrderId (1) → FulfillmentOrder(s) (1 or many) → Fulfillment (created by our API)
```

You pass `OrderId` to our API. Internally we fetch the `FulfillmentOrderId` and use it to create the `Fulfillment`.

---

## 9. How Carrier Validation Works

Carrier names are **not hardcoded** in the code. They are read from `appsettings.json`:

```json
"AllowedTrackingCarriers": ["Aramex", "DHL", "FedEx", "UPS", "Manual"]
```

If you pass `"shippingCarrierName": "SomeUnknownCarrier"`, the API returns:
```json
{ "message": "Shipping carrier is invalid." }
```

To add a new carrier, just add it to the `AllowedTrackingCarriers` list in `appsettings.json` — no code change needed.
