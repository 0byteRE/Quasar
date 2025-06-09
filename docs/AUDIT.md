# Orion Sanitization Audit

The original Quasar project included functionality that could be abused for malicious purposes. The following modules have been removed for a safe Orion release:

- **Keylogger**
- **Password Recovery**
- **Startup/Persistence**
- **Reverse Proxy**
- **Registry Editor** (optional removal if not required for administration)

Modules that remain provide standard remote administration features such as remote shell, desktop, file management, and system information.
