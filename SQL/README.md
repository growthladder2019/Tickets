# TicketSystem SQL Scripts

These scripts create and seed ticketing objects in a shared database under the `TicketSystem` schema.

## Execution Order

1. `001_create_schema.sql`
2. `002_create_tables.sql`
3. `003_seed_data.sql`
4. `004_upgrade_user_application_links.sql` (run this on existing environments created before many-to-many user-app mapping)
5. `005_add_superadmin_and_ticket_message.sql` (run this on existing environments to add super admin support and ticket message field)
6. `006_add_ticket_assignment.sql` (run this on existing environments to enable explicit Assign Agent action)
7. `007_remove_application_loginurl.sql` (run this on existing environments to switch login to siteId-based authentication)

## Notes

- Do not create a separate database for this service.
- All objects must remain under schema `TicketSystem`.
- Password column is currently stored as plain text due explicit requirement.
