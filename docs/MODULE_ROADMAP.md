# ShaunERP module build order

Confirmed product sequence:

1. **User Management** (done)
2. **CRM** (done)
3. **Invoicing** (done — Finance lite)
4. **Sales Orders** (done — order-to-cash)
5. **Accounting** (done — company config, double-entry engine, reports, invoice posting)
6. **Project Management** (done — P0 core + P1 foundations: tasks, kanban, my work, timesheets, issues/risks/CRs, templates, SO→Project)

Each module ships as: API + UI screens + permissions + deploy to `erp.shauninfomedia.com`.

Later modules / PM P2: Inventory (operations), Purchase, HRMS, Banking reconciliation, GST returns, Reporting; PM Gantt library, resource capacity views, invoice-from-approved-timesheets.
