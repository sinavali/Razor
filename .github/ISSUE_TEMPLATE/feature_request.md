name: Feature request
about: Suggest an idea for this project
title: ''
labels: enhancement
assignees: ''

body:
  - type: markdown
    attributes:
      value: |
        Thanks for suggesting a feature.
  - type: textarea
    id: problem
    attributes:
      label: Problem
      description: What problem does this solve?
      placeholder: I'm frustrated when ...
    validations:
      required: true
  - type: textarea
    id: proposal
    attributes:
      label: Proposal
      description: Describe the solution you'd like.
    validations:
      required: true
  - type: textarea
    id: principles
    attributes:
      label: Principles alignment
      description: How does this align with the repository principles?
      placeholder: e.g. supports determinism / parity / auditability
