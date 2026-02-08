# Menu Management User Stories

Stories extracted from unit test specifications covering menu categories, menu items, modifiers, POS menu definitions, menu CMS (content management with versioning), menu registry, and accounting groups.

---

## Menu Categories

**As a** menu manager, **I want to** create a new menu category with display settings, **So that** I can organize menu items into logical groups for staff and guests.
- Given: no existing menu category
- When: a new "Starters" category is created with a display order, color, and description
- Then: the category is active with the correct name, description, display order, color, and zero items

**As a** menu manager, **I want to** update an existing menu category's details, **So that** I can refine how categories are named, described, and ordered over time.
- Given: an existing "Starters" menu category
- When: the category name, description, display order, and color are updated
- Then: the category reflects the new name "Appetizers", updated description, reordered position, and new color

**As a** menu manager, **I want to** deactivate a menu category, **So that** seasonal or retired groupings no longer appear on active menus.
- Given: an active "Seasonal" menu category
- When: the category is deactivated
- Then: the category is marked as inactive

**As a** menu manager, **I want to** track how many items belong to a category, **So that** I can see at a glance which categories are populated and which need attention.
- Given: a "Mains" category with zero items
- When: three menu items are added to the category
- Then: the category item count is 3

---

## Menu Items

**As a** menu manager, **I want to** create a new menu item with pricing and inventory settings, **So that** the item can be sold and optionally tracked against stock.
- Given: no existing menu item
- When: a "Caesar Salad" is created at $12.99 with inventory tracking and a SKU
- Then: the item is active with correct name, price, SKU, inventory tracking enabled, and no modifiers

**As a** menu manager, **I want to** update an existing menu item's details, **So that** I can adjust naming, pricing, and tracking as the business evolves.
- Given: an existing "House Burger" menu item at $14.99 without inventory tracking
- When: the item name, description, price, and inventory tracking are updated
- Then: the item reflects the new name "Classic Burger", updated price of $15.99, and inventory tracking enabled

**As a** menu manager, **I want to** deactivate a menu item, **So that** discontinued or out-of-season items stop appearing on the menu.
- Given: an active "Seasonal Special" menu item
- When: the item is deactivated
- Then: the item is marked as inactive

**As a** menu manager, **I want to** record a menu item's theoretical food cost, **So that** I can monitor cost percentages and maintain healthy margins.
- Given: a "Pasta Carbonara" priced at $18.99 with a linked recipe
- When: the theoretical food cost is updated to $5.75
- Then: the item shows a cost of $5.75 and a cost percentage of approximately 30.28%

**As a** system, **I want to** reject menu items created with a negative price, **So that** invalid pricing data never enters the system.
- Given: no existing menu item
- When: a menu item is created with a negative price of -$5.00
- Then: the operation is rejected with a validation error about negative prices

**As a** system, **I want to** reject menu items created with an empty name, **So that** every item is identifiable by staff and guests.
- Given: no existing menu item
- When: a menu item is created with an empty name
- Then: the operation is rejected with a validation error about empty names

---

## Modifiers

**As a** menu manager, **I want to** add a required modifier group with options to a menu item, **So that** staff must capture essential customizations like size when placing an order.
- Given: a "Coffee" menu item with no modifiers
- When: a required "Size" modifier with Small, Medium, and Large options is added
- Then: the item has one modifier group with 3 options and the modifier is marked as required

**As a** menu manager, **I want to** attach multiple modifier groups to a single item, **So that** both required and optional customizations are captured together.
- Given: a "Pizza" menu item with no modifiers
- When: a required "Size" modifier and an optional "Extra Toppings" modifier are both added
- Then: the item has two modifier groups, one required and one optional

**As a** menu manager, **I want to** remove a modifier group from a menu item, **So that** outdated customization options no longer appear during order entry.
- Given: a "Steak" menu item with a required "Temperature" modifier (Rare, Medium, Well Done)
- When: the temperature modifier is removed
- Then: the item has no modifiers

**As a** system, **I want to** reject a modifier group that has zero options, **So that** every modifier presents at least one selectable choice.
- Given: an existing menu item
- When: a modifier group with zero options is added
- Then: the operation is rejected because a modifier must have at least one option

**As a** system, **I want to** reject a modifier where minimum selections exceed maximum selections, **So that** impossible selection constraints are never configured.
- Given: an existing menu item
- When: a modifier is added with minimum selections (5) exceeding maximum selections (2)
- Then: the operation is rejected because min selections cannot exceed max selections

---

## POS Menu Definitions

**As a** venue operator, **I want to** create a POS menu definition for a specific order type, **So that** the tablet interface is tailored to how orders are taken.
- Given: no existing POS menu definition
- When: a new "Main POS Menu" is created as the default menu for dine-in orders
- Then: the menu is active, set as default, and has no screens

**As a** venue operator, **I want to** add a screen with pre-configured item buttons to a POS menu, **So that** staff can quickly tap common items without searching.
- Given: a menu definition with no screens
- When: a "Drinks" screen is added with 3 pre-configured item buttons (Coffee, Tea, Soda)
- Then: the screen contains all 3 buttons with correct labels and linked menu item IDs

**As a** venue operator, **I want to** place an item button at a specific grid position on a screen, **So that** the POS layout matches the physical or logical flow staff expect.
- Given: a menu screen with a 4x6 grid and no buttons
- When: a "Burger" item button is placed at row 1, column 2
- Then: the screen has one button at the specified grid position with the correct label

**As a** venue operator, **I want to** add navigation buttons that link to sub-screens, **So that** staff can drill into category-specific screens from a main menu.
- Given: a menu with a main screen and a "Drinks" sub-screen
- When: a navigation button linking to the drinks sub-screen is added to the main screen
- Then: the button is typed as "Navigation" and references the drinks sub-screen ID

---

## Menu Item Documents (CMS)

**As a** menu manager, **I want to** create and immediately publish a menu item document, **So that** the item is live on menus as soon as it is entered.
- Given: no existing menu item document
- When: a "Caesar Salad" at $12.99 is created with immediate publication
- Then: the document is at version 1, published with the correct name and price, and has no draft

**As a** menu manager, **I want to** create a menu item as a draft without publishing, **So that** I can prepare items ahead of time before making them visible.
- Given: no existing menu item document
- When: a "Draft Item" is created without immediate publication
- Then: the document exists only as a draft at version 1 with no published version

**As a** menu manager, **I want to** create a new draft on top of a published item, **So that** I can stage changes without affecting what guests currently see.
- Given: a published menu item document at version 1
- When: a new draft is created with an updated name, price, and change note
- Then: the draft is at version 2 with the new values while the published version remains at version 1

**As a** menu manager, **I want to** publish a pending draft, **So that** staged changes go live in a single controlled action.
- Given: a published menu item with a pending draft containing an updated name and price
- When: the draft is published
- Then: the published version advances to version 2 with the draft's content and the draft is cleared

**As a** menu manager, **I want to** discard a pending draft, **So that** unwanted changes are abandoned without affecting the live menu.
- Given: a published menu item with a pending draft at an extreme price
- When: the draft is discarded
- Then: the draft is removed, version count returns to 1, and the original published version remains

**As a** menu manager, **I want to** revert a menu item to a previous version, **So that** I can quickly roll back a mistake without manually re-entering old content.
- Given: a menu item with version 1 ($10) and version 2 ($20) both published
- When: the item is reverted to version 1
- Then: a new version 3 is created with version 1's content ($10) and the total version count is 3

**As a** menu manager, **I want to** retrieve the full version history of a menu item, **So that** I can audit every change that has been made over time.
- Given: a menu item document that has been published through 3 versions
- When: the version history is retrieved
- Then: all 3 versions are returned in reverse chronological order (newest first)

**As a** menu manager, **I want to** add a translation to a published menu item, **So that** guests and staff who speak other languages see localized content.
- Given: a published "Chicken" menu item in English
- When: a Spanish (es-ES) translation is added with name, description, and kitchen name
- Then: the published version includes the Spanish translation with all translated fields

**As a** menu manager, **I want to** schedule a pricing version to activate at a future time, **So that** happy hour or promotional pricing takes effect automatically.
- Given: a menu item with a published version and a happy hour pricing version
- When: the happy hour version is scheduled to activate tomorrow
- Then: an active schedule entry is created targeting the correct version and activation time

---

## Menu Registry - Items

**As a** menu manager, **I want to** register a menu item in the organization's registry, **So that** the item is discoverable across all sites and POS configurations.
- Given: an empty menu registry for an organization
- When: a "Caesar Salad" item at $12.99 is registered in category "category-1"
- Then: the registry contains the item with correct name, price, category, and default flags

**As a** menu manager, **I want to** re-register an item with updated details, **So that** the registry always reflects the latest published content.
- Given: a registered menu item "Original Name" already registered in the registry
- When: a new item is registered with the same document ID but different name and price
- Then: the existing entry is replaced with the updated name, price, and category

**As a** menu manager, **I want to** archive a registered menu item, **So that** it is hidden from everyday views but preserved for historical reference.
- Given: a registered menu item
- When: the item is updated with isArchived=true
- Then: the item is excluded from default queries but included when includeArchived=true

**As a** menu manager, **I want to** unregister a menu item from the registry, **So that** permanently removed items no longer clutter the catalog.
- Given: a registered menu item in the registry
- When: the item is unregistered
- Then: the item is completely removed from the registry

**As a** menu manager, **I want to** filter registered items by category, **So that** I can quickly find items within a specific section of the menu.
- Given: four items registered across "cat-food", "cat-drinks", and uncategorized
- When: items are queried filtered by "cat-food" and "cat-drinks" respectively
- Then: only items matching the requested category are returned

---

## Menu Registry - Categories

**As a** menu manager, **I want to** register a menu category in the organization's registry, **So that** category metadata is centrally managed and available to all sites.
- Given: an empty menu registry for an organization
- When: an "Appetizers" category is registered with display order 1 and a color
- Then: the registry contains the category with correct metadata and default flags

**As a** menu manager, **I want to** retrieve categories sorted by display order, **So that** the menu structure is presented in the intended sequence.
- Given: three categories registered out of display order (Desserts=3, Appetizers=1, Main Courses=2)
- When: categories are queried from the registry
- Then: categories are returned sorted by display order ascending

**As a** menu manager, **I want to** archive a menu category, **So that** retired categories are hidden from active views but preserved for reporting.
- Given: a registered menu category
- When: the category is updated with isArchived=true
- Then: the category is excluded from default queries but included when includeArchived=true

---

## Menu Registry - Modifiers & Tags

**As a** menu manager, **I want to** register a modifier block in the organization's registry, **So that** shared modifier configurations are centrally tracked.
- Given: an empty menu registry
- When: a "Size Options" modifier block is registered
- Then: the block ID appears in the registry's modifier block list

**As a** menu manager, **I want to** register a content tag in the organization's registry, **So that** dietary, allergen, and promotional labels are managed in one place.
- Given: an empty menu registry
- When: a "Gluten Free" dietary tag is registered
- Then: the tag ID appears in the registry's tag list

**As a** menu manager, **I want to** filter tags by category, **So that** I can view only the type of tags relevant to my current task.
- Given: tags registered across Dietary (2), Allergen (1), and Promotional (1) categories
- When: tags are queried filtered by each category
- Then: only tags matching the requested category are returned

**As a** system, **I want to** enforce tenant isolation in the menu registry, **So that** one organization's menu data is never visible to another.
- Given: two separate organizations each with their own menu registry
- When: items and categories are registered in each organization's registry
- Then: each organization only sees its own data (tenant isolation)

---

## Accounting Groups

**As a** finance manager, **I want to** create an accounting group with revenue and cost account codes, **So that** menu item sales are mapped to the correct ledger accounts for reporting.
- Given: no existing accounting group
- When: a "Food Sales" accounting group is created with revenue and COGS account codes
- Then: the group is active with correct name, code, accounts, and zero item count

**As a** finance manager, **I want to** assign menu items to an accounting group, **So that** each item's revenue and cost flow to the appropriate accounts.
- Given: a "Merchandise" accounting group with zero items
- When: two menu items are assigned to the group
- Then: the group item count is 2

**As a** finance manager, **I want to** remove a menu item from an accounting group, **So that** reassigned or deleted items no longer affect the group's reporting.
- Given: an "Alcohol" accounting group with 3 assigned items
- When: one item is removed from the group
- Then: the group item count decreases to 2
