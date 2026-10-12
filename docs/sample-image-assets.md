# Homepage sample photography

These are **temporary stock photographs**, not client inventory, product brands, exact packaging, variant quantities or offers. Sample names/prices remain presentation fixtures. Availability is a manually specified demo boolean, with Sobolo demonstrating the unavailable state; no stock tracking is implied. Add to Cart opens a notice explaining that no item was added. Replace stock photos with approved client packshots before live sales.

Downloaded on 10 October 2026 from Pexels. The [Pexels License](https://www.pexels.com/license/) permits website use and modifications without required attribution; it prohibits implying endorsement and redistributing images as a stock-photo service. Credits and source links are retained below. No brands or endorsements are claimed.

| Local asset in `wwwroot/images/samples/` | Photographer | Source | Usage / limitation |
| --- | --- | --- | --- |
| `hero.jpg`, `hero-thumb.jpg` | MART PRODUCTION | [Grocery basket, 7890015](https://www.pexels.com/photo/assorted-vegetables-in-a-basket-and-on-a-wooden-table-7890015/) | Hero / all essentials category; produce is illustrative, not confirmed inventory |
| `rice.jpg`, `rice-thumb.jpg` | Markus Winkler | [Rice, 13796760](https://www.pexels.com/photo/food-healthy-light-dinner-13796760/) | Rice / pantry / supporting panel; loose grains, not a 5 kg branded bag |
| `oil.jpg`, `oil-thumb.jpg` | Towfiqu barbhuiya | [Cooking oil, 12284682](https://www.pexels.com/photo/clear-plastic-bottles-with-yellow-liquid-12284682/) | Oil / cooking category; bottles are not the sample's exact 1 litre variant |
| `beans.jpg`, `beans-thumb.jpg` | Marina Leonova | [White beans, 7717475](https://www.pexels.com/photo/close-up-shot-of-white-beans-7717475/) | Loose white beans, not an actual 1 kg pack |
| `eggs.jpg`, `eggs-thumb.jpg` | Klaus Nielsen | [Egg carton, 6294168](https://www.pexels.com/photo/set-of-chicken-eggs-in-carton-box-6294168/) | Eggs / dairy category; photograph does not depict the 30-egg sample quantity |
| `milk.jpg`, `milk-thumb.jpg` | Charlotte May | [Milk, 5946755](https://www.pexels.com/photo/glass-bottle-with-milk-on-table-5946755/) | Glass bottle represents milk; not the sample's exact carton or confirmed whole-milk product |
| `noodles.jpg`, `noodles-thumb.jpg` | Polina Tankilevitch | [Instant noodles, 4518665](https://www.pexels.com/photo/photo-of-uncooked-noodles-on-white-background-4518665/) | Noodles / quick meals category; unpackaged food, not five branded sachets |
| `tomato.jpg`, `tomato-thumb.jpg` | elif tekkaya | [Tomato paste, 4361645](https://www.pexels.com/photo/tomato-paste-in-ceramic-bowl-4361645/) | Paste in bowl with tomatoes, not an actual 400 g tin |
| `sobolo.jpg`, `sobolo-thumb.jpg` | sha ima | [Hibiscus tea, 8678927](https://www.pexels.com/photo/a-hibiscus-tea-8678927/) | Drinks category / Sobolo illustration; generic hibiscus tea, not a verified Ghanaian recipe or bottled product |

## Delivery and presentation

- Local image delivery, with versioned URLs; no runtime photo-host dependency.
- Hero: 1000 × 694 pixels, eager loading with high fetch priority, `object-fit: cover` within the existing hero frame.
- Product images: 600 × 450 pixels, consistent 4:3 frame, `object-fit: contain`, meaningful stock-photo alt descriptions. Pexels delivery crops are temporary; final packshots should show complete packaging.
- Category thumbnails: 128 × 128 source pixels displayed at 32 × 32 pixels; decorative empty alt text alongside visible category names avoids duplicate announcements.
- All 15 images below the hero use lazy loading and asynchronous decoding. Width/height attributes reserve space. Unused product thumbnail derivatives are retained for reuse; only six category thumbnails are requested by this page.
- Existing typography, colour tokens, page grid, breakpoints and section spacing are retained. Product card content necessarily grows to accommodate availability and an accessible 44px Add to Cart control. No database, authentication, payment or live cart was added.

## Verification

Production Tailwind compilation and .NET 8 build pass with zero .NET warnings/errors. Headless Edge previews were visually inspected at 375px, 768px and 1440px: no horizontal overflow, all 16 displayed images decoded, all eight product frames are 4:3, and Inter loaded. Confirmed the hero's high fetch priority, 15 lazy-loaded images, sample search/filter/reset/empty states, mobile navigation, preview dialogs, Add to Cart's explicit no-item-added notice and one disabled sample product. The pantry badge retains its circular shape on tablet. Full browser coverage and live-commerce acceptance remain future work.
