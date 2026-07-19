name: Bug report
about: Create a report to help us improve
title: ''
labels: bug
assignees: ''

body:
  - type: markdown
    attributes:
      value: |
        Thanks for taking the time to fill out this bug report.
  - type: input
    id: environment
    attributes:
      label: Environment
      description: OS, .NET version, browser, etc.
      placeholder: e.g. Windows 11, .NET 10.0.1
    validations:
      required: true
  - type: textarea
    id: steps
    attributes:
      label: Steps to reproduce
      description: How can we reproduce the issue?
      placeholder: |
        1. Run '...'
        2. Configure '...'
        3. See error
    validations:
      required: true
  - type: textarea
    id: expected
    attributes:
      label: Expected behavior
      description: What should happen?
    validations:
      required: true
  - type: textarea
    id: actual
    attributes:
      label: Actual behavior
      description: What actually happens?
    validations:
      required: true
  - type: textarea
    id: logs
    attributes:
      label: Logs / error output
      description: Paste relevant logs or stack traces.
      render: shell
