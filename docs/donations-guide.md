# Setting up donations

Step by step: which options to set up, what they charge, and how they reach the
app and GitHub. Figures are as of September 2026; check the official pages
linked below before you start, since terms change.

## Where people see them

You enter every option once in `release-settings.ini`, and `build-release.cmd`
puts it everywhere:

- **The app:** **Settings › Support**: links open with a button, wallet
  addresses are copied.
- **GitHub:** the last README section, "Support the project", with every
  address. That's your "all the ways to support" page; you can link it
  anywhere: `https://github.com/<you>/<repository>#donate`.
- **The Sponsor button** at the top of the repository page: Ko-fi, Buy Me a
  Coffee and a link to the same README section.

## 1. Choose your options

People can pay from anywhere; the limit is where **you** receive the money.

| Option | Works if | Fees | How the money arrives |
|---|---|---|---|
| **Ko-fi** | PayPal or Stripe works for you | 0% on one-off tips (after turning off Contributor mode, see below) plus PayPal/Stripe fees, about 3% + $0.30 | Straight to your PayPal or Stripe |
| **Buy Me a Coffee** | Stripe pays out in your country. Not available: Russia, Belarus, Ukraine, Georgia. Available, for example: Kazakhstan, Armenia, Serbia, the EU, the US | 5% plus Stripe fees (2.9% + $0.30, +1% for payments from outside the US) | Via Stripe to your bank account, from $10 |
| **USDT / USDC on EVM networks** | Any country | The network charges the sender; cents on Polygon, Arbitrum, Base, BNB Chain | To your wallet |
| **USDT (TRC20)** | Any country | The network charges the sender (usually $2–4) | To your wallet |
| **Solana** (USDC, SOL) | Any country | The network charges the sender, fractions of a cent | To your wallet |
| **Bitcoin** | Any country | The network charges the sender | To your wallet |

A sensible set: one card-payment platform (Ko-fi or Buy Me a Coffee), one EVM
address and, if you like, TRC20, Solana and Bitcoin; many people hold USDT on
TRON.

Official pages:
[Ko-fi pricing](https://ko-fi.com/pricing),
[Buy Me a Coffee payout countries](https://help.buymeacoffee.com/en/articles/6258038-supported-countries-for-payouts-on-buy-me-a-coffee),
[Buy Me a Coffee fees](https://help.buymeacoffee.com/en/articles/8105744-how-to-calculate-charges-on-your-payment).

## 2. Ko-fi

1. Sign up at [ko-fi.com](https://ko-fi.com) and pick a short page name; it
   becomes your address `https://ko-fi.com/<name>`.
2. **Settings › Payments:** connect PayPal (a PayPal business account is needed;
   a personal one converts for free) or Stripe. Money goes straight there;
   Ko-fi doesn't hold it.
3. New accounts start in **Contributor** mode, which takes 5% of one-off tips
   too. If you don't need its features, turn it off in your account settings to
   get one-off tips without a Ko-fi fee.
4. Fill in the page: a couple of lines about SuperDictate and a link to the
   repository.
5. Test it: open your page in a private window and send yourself a minimal tip
   from another card.

## 3. Buy Me a Coffee

1. First check your country in the
   [payout country list](https://help.buymeacoffee.com/en/articles/6258038-supported-countries-for-payouts-on-buy-me-a-coffee).
   If it isn't there, this option won't work for you.
2. Sign up at [buymeacoffee.com](https://buymeacoffee.com); your page is
   `https://buymeacoffee.com/<name>`.
3. Connect Stripe (Stripe Express): you'll need an ID and a bank account.
4. Payouts start at $10; after approval the money goes to Stripe, then to your
   account.
5. Check the page in a private window.

## 4. Crypto

### One address for many networks (EVM)

Ethereum, BNB Chain, Polygon, Arbitrum, Base, Optimism and other EVM networks
all use **the same address** `0x…`. Set it up once and you receive USDT, USDC
and those networks' coins on all of them: a multi-network address.

- Use **your own** wallet that supports all these networks, for example
  MetaMask, Rabby or Trust Wallet. The address is 42 characters and starts
  with `0x`.
- If someone sends on a network your wallet doesn't show yet, the money isn't
  lost: add that network in the wallet and it appears; the key is the same.
- **Don't use an exchange deposit address** as your EVM address: the exchange
  only credits networks on its list, and a transfer on another network can be
  lost.

TRON, Bitcoin, Solana and TON use different addresses, one line per network.
Wallets like Trust Wallet give you addresses for all of them from one recovery
phrase; just copy the address for the network you need.

### Other networks

- **USDT (TRC20):** a TRON address starts with `T` and has 34 characters
  (TronLink, Trust Wallet).
- **Bitcoin:** an address of the form `bc1…` (Electrum, BlueWallet, Trust
  Wallet).
- **Solana:** an address of 32–44 letters and digits (Phantom, Solflare, Trust
  Wallet). One address receives both SOL and USDC on Solana.
- Need another network (TON, for example)? Add a line to
  `release-settings.ini`; see section 5.

### Safety: required reading

- When you create a wallet it shows a **recovery phrase** of 12 or 24 words.
  Write it on paper and keep it offline. Don't photograph it or keep it in the
  cloud, notes or messengers. Whoever knows the phrase owns the money.
- Publish **only the address**; that's safe. Nobody needs the phrase or the
  private key, including "support": such requests are scams.
- After copying an address, compare its first and last 5 characters. Malware
  swaps addresses in the clipboard.
- Make a **small test transfer** on every network you list and make sure it
  arrives, before releasing the app.
- A blockchain address is public: anyone can see every payment to it. If you
  don't want that, create a separate wallet just for donations.
- Always name the network in the note: USDT sent on TRON to a `0x…` address (or
  the other way round) won't arrive.

## 5. Enter the options in release-settings.ini

Open `release-settings.ini` in the project root (in Notepad) and fill in the
`[donations]` section. Each line is:

```ini
Name | note under the name = link or address
```

**The answer always goes at the end of the line, after "=".** Example:

```ini
[donations]
Ko-fi | Card or PayPal = https://ko-fi.com/yourname
Buy Me a Coffee | Card = https://buymeacoffee.com/yourname
USDT / USDC (EVM) | Same address on Ethereum, BNB Chain, Polygon, Arbitrum, Base = 0x1234567890abcdef1234567890abcdef12345678
USDT (TRC20) | TRON network only = TXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
Solana | USDC or SOL on Solana only = xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
Bitcoin | Bitcoin network only = bc1qxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

- A line without an answer isn't shown anywhere. Delete lines you don't need,
  add your own. If all are empty, the app doesn't ask for donations.
- Names and notes appear in the app and on GitHub.
- A link to any page of yours (with all options, say) works too:
  `All options | Cards and crypto = https://...`.

Then run `build-release.cmd`. It checks every line and lists what to fix: a link
must be complete (`https://ko-fi.com/<name>`, not just `https://ko-fi.com/`), an
address must have no spaces, and the answer must come after "=".

## 6. Check

1. In the newly installed build, open **Settings › Support** and click every
   button: links lead to your pages, and copied addresses match the ones in
   your wallet.
2. After pushing to GitHub, open the repository: at the end of the README is
   "Support the project" with your options. For the **Sponsor** button, turn on
   **Sponsorships** in the repository's Settings › General › Features.

## 7. Taxes

In most countries donations count as income. How to declare them depends on
your country and status; find out before the first ones arrive.
