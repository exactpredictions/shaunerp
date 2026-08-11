# Cloudflare DNS checklist — shauninfomedia.com

Do this in Cloudflare **after** nginx is serving on the Lightsail public IP.

## 1. Find Lightsail public IP

Lightsail console → instance → Networking → Public IP  
Example shape: `x.x.x.x` (use the real one, not Tailscale).

## 2. DNS records

| Type | Name | Content | Proxy status |
|------|------|---------|--------------|
| A | `@` | `<LIGHTSAIL_PUBLIC_IP>` | DNS only (grey) first |
| A | `www` | `<LIGHTSAIL_PUBLIC_IP>` | DNS only first |
| A | `erp` | `<LIGHTSAIL_PUBLIC_IP>` | DNS only first |

Wait 1–5 minutes, then test:

```bash
curl -I http://shauninfomedia.com
curl -I http://erp.shauninfomedia.com
```

## 3. Certbot (on server)

```bash
sudo certbot --nginx -d shauninfomedia.com -d www.shauninfomedia.com -d erp.shauninfomedia.com
```

## 4. Turn Cloudflare proxy back on (optional)

Orange cloud on `@`, `www`, `erp`.

Cloudflare SSL/TLS → **Full (strict)**.

## 5. Email (optional later)

MX / mail records are separate — not required for the website or ERP demo.
