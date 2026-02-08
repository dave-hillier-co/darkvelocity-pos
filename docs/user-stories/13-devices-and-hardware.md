# Devices & Hardware User Stories

Stories extracted from unit test specifications covering POS device registration, printer management, device heartbeats, and peripheral configuration.

## POS Device Registration

**As a** site administrator,
**I want to** register a new POS tablet with full hardware details including model, OS, and app version,
**So that** the device is tracked and ready for use at the site location.

- Given: a new POS tablet device with full registration details including model, OS, and app version
- When: the device is registered at a site location
- Then: the device is created as active and online with all properties set and default settings enabled

**As a** site administrator,
**I want to** register a POS mobile device with only the required fields,
**So that** lightweight devices can be onboarded without needing every hardware detail up front.

- Given: a new POS mobile device with only required registration fields
- When: the device is registered without optional model, OS, or app version
- Then: the device is created as active with null optional properties

**As a** site administrator,
**I want to** be prevented from registering the same device twice,
**So that** duplicate device records do not cause confusion or conflicts.

- Given: a POS device that has already been registered
- When: a second registration is attempted on the same device grain
- Then: an exception is thrown indicating the device is already registered

## Device Configuration

**As a** site administrator,
**I want to** update all configurable fields on a POS device including name, model, versions, peripherals, and settings,
**So that** the device record stays accurate as hardware or software changes.

- Given: a registered POS device
- When: all configurable fields are updated including name, model, versions, peripherals, and settings
- Then: all fields reflect the new values

**As a** site administrator,
**I want to** assign a default printer to a POS device,
**So that** receipts and tickets print automatically without staff selecting a printer each time.

- Given: a registered POS device with no default printer assigned
- When: a default printer ID is set via update
- Then: the device is linked to the specified printer

**As a** site administrator,
**I want to** assign a default cash drawer to a POS terminal,
**So that** the correct drawer opens automatically during cash transactions.

- Given: a registered POS terminal with no default cash drawer assigned
- When: a default cash drawer ID is set via update
- Then: the device is linked to the specified cash drawer

**As a** site administrator,
**I want to** disable auto-print receipts on a specific device,
**So that** certain stations can operate without printing unless explicitly requested.

- Given: a registered POS device with auto-print receipts enabled by default
- When: the auto-print receipts setting is disabled via update
- Then: the auto-print receipts setting is false

## Device Heartbeat & Status

**As a** site administrator,
**I want to** receive updated app and OS version information through device heartbeats,
**So that** the system always reflects the current software running on each device.

- Given: a registered POS device with existing app and OS versions
- When: a heartbeat is received with updated app and OS versions
- Then: both versions are updated, the device remains online, and last-seen is refreshed

**As a** site administrator,
**I want to** have a device automatically come back online when it sends a heartbeat after being offline,
**So that** device availability is tracked without manual intervention.

- Given: a registered POS device that has been set to offline
- When: a heartbeat is received
- Then: the device comes back online

**As a** site administrator,
**I want to** mark a device as offline,
**So that** the system reflects that the device is no longer actively in use.

- Given: a registered POS device that is currently online
- When: the device is set to offline
- Then: the device reports as offline

**As a** site administrator,
**I want to** register multiple devices at the same site with independent configurations,
**So that** each terminal operates with its own identity and settings.

- Given: two POS devices registered under the same organization and location
- When: each device is configured with different names and types
- Then: each device grain maintains independent state

## Device Deactivation

**As a** site administrator,
**I want to** deactivate a POS device that is being retired,
**So that** it is no longer considered part of the active device fleet.

- Given: a registered active POS device
- When: the device is deactivated
- Then: the device becomes inactive

**As a** site administrator,
**I want to** have a deactivated device automatically go offline,
**So that** inactive devices are not mistakenly shown as available.

- Given: a registered POS device that is currently online
- When: the device is deactivated
- Then: the device is set to both inactive and offline

**As a** site administrator,
**I want to** reactivate a previously deactivated device,
**So that** hardware can be returned to service without re-registering it.

- Given: a registered POS device that has been deactivated
- When: the device is reactivated by setting IsActive to true via update
- Then: the device becomes active again

## Printer Registration

**As a** site administrator,
**I want to** register a network receipt printer with IP address, port, and paper width as the site default,
**So that** the printer is available for receipt printing across the site.

- Given: a new network receipt printer with IP address, port, and 80mm paper width
- When: the printer is registered as the site default
- Then: the printer is created as active with all network properties set and starts offline

**As a** site administrator,
**I want to** register a USB receipt printer with vendor and product identifiers,
**So that** the system can address the printer through its USB connection.

- Given: a new USB receipt printer with vendor and product IDs
- When: the printer is registered with USB connection type
- Then: the USB identifiers are stored and network properties remain null

**As a** site administrator,
**I want to** register a Bluetooth receipt printer with a MAC address,
**So that** the system can discover and connect to the printer wirelessly.

- Given: a new Bluetooth receipt printer with a MAC address
- When: the printer is registered with Bluetooth connection type
- Then: the MAC address is stored correctly

**As a** site administrator,
**I want to** register a kitchen printer assigned to the hot line,
**So that** kitchen tickets route to the correct station printer.

- Given: a new network kitchen printer for the hot line
- When: the printer is registered as Kitchen type
- Then: the printer type is set to Kitchen

**As a** site administrator,
**I want to** be prevented from registering the same printer twice,
**So that** duplicate printer records do not cause routing or configuration conflicts.

- Given: a printer that has already been registered
- When: a second registration is attempted on the same printer grain
- Then: an exception is thrown indicating the printer is already registered

## Printer Configuration

**As a** site administrator,
**I want to** update all configurable properties on a printer including name, IP, port, character set, and capabilities,
**So that** the printer record stays accurate after network or hardware changes.

- Given: a registered network receipt printer
- When: all configurable properties are updated including name, IP, port, character set, and capabilities
- Then: all updated properties reflect the new values

**As a** site administrator,
**I want to** enable paper cut and cash drawer support on a receipt printer,
**So that** the printer can automatically cut receipts and trigger the cash drawer.

- Given: a registered receipt printer without cash drawer support
- When: paper cut and cash drawer support capabilities are enabled via update
- Then: both capabilities are set to true

**As a** site administrator,
**I want to** change the character set on a receipt printer,
**So that** printed text renders correctly for the locale in use.

- Given: a registered receipt printer with default character set
- When: the character set is updated to ISO-8859-1
- Then: the character set reflects the new value

**As a** site administrator,
**I want to** ensure printer configurations are isolated between organizations,
**So that** one tenant's printer settings cannot affect another tenant's hardware.

- Given: two different organizations each with the same printer ID
- When: each organization registers its printer with different configurations
- Then: the printers are isolated by tenant boundary and maintain independent state

**As a** site administrator,
**I want to** register a printer with an IPv6 network address,
**So that** modern network configurations are supported.

- Given: a new network printer
- When: the printer is registered with an IPv6 address
- Then: the IPv6 address is stored correctly

## Printer Status & Print Tracking

**As a** site administrator,
**I want to** have a printer automatically come online when it completes a print job,
**So that** print activity serves as a proof of connectivity.

- Given: a registered receipt printer that starts offline
- When: a print is recorded
- Then: the last-print timestamp is set and the printer is marked online

**As a** site administrator,
**I want to** have an offline printer transition to online when a print succeeds,
**So that** the system reflects actual printer availability without manual status changes.

- Given: a registered receipt printer that is currently offline
- When: a print is successfully recorded
- Then: the printer is automatically set to online

**As a** site administrator,
**I want to** manually set a printer to online,
**So that** connectivity can be confirmed through a ping or test print.

- Given: a registered printer that starts offline
- When: the printer is set to online
- Then: the printer reports as online

**As a** site administrator,
**I want to** know that newly registered printers default to offline,
**So that** no printer is assumed available until communication is confirmed.

- Given: a newly registered receipt printer
- When: the online status is checked immediately after registration
- Then: the printer is offline (printers start offline until first print or ping)
