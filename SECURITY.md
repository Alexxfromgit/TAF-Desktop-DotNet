# Security policy

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Use GitHub's
[private vulnerability reporting](https://github.com/Alexxfromgit/TAF-Desktop-DotNet/security/advisories/new) instead.
You will get a response within a few days.

## Secrets in tests

- Passwords and other secrets are read only from environment variables (`TAF__...`).
- Configuration loading fails if `taf.json` or `taf.{env}.json` contains a secret-like value, and the metadata
  linter checks the same.
- Typed passwords are masked in report steps and left out of UI tree dumps.

If you find a way these safeguards can be bypassed, please report it as above.
