# Minimal Greenfield Structure

Apply this layout to an empty (or nearly empty) repository before any feature code is written. The `backend/` and `frontend/` folders stay empty during bootstrap — they are populated by the first scaffolding stories, not by the bootstrap itself.

```text
.
├── AGENTS.md
├── package.json                    # root workflow orchestrator (validate, story-map)
├── .github/
│   └── workflows/
│       └── ci.yml
├── backend/                        # .NET solution + projects (created by first story)
├── frontend/                       # React + Vite + TS app (created by first story)
├── e2e/
│   ├── package.json
│   ├── playwright.config.ts
│   └── tests/
│       └── smoke.spec.ts
├── docs/
│   ├── development.md
│   └── product-vision.md
├── scripts/
│   ├── generate-story-map.js
│   └── validate-workflow.js
├── specs/
│   ├── index.md
│   ├── 00-foundations.spec.md
│   ├── 01-routing-and-navigation.spec.md
│   ├── 02-auth-and-identity.spec.md
│   ├── 03-core-workflows.spec.md
│   ├── 04-data-and-persistence.spec.md
│   ├── 05-integrations.spec.md
│   ├── 06-testing-and-qa.spec.md
│   ├── 07-deployment-and-ops.spec.md
│   └── 08-visual-design.spec.md
└── stories/
    ├── 00-story-map.md
    ├── _template.story_TODO.md
    ├── 01-scaffold-dotnet-solution.story_TODO.md
    ├── 02-scaffold-react-frontend.story_TODO.md
    ├── 03-docker-compose-and-postgres.story_TODO.md
    ├── 04-ef-core-baseline-and-first-migration.story_TODO.md
    ├── 05-ci-pipeline.story_TODO.md
    ├── 06-e2e-smoke-test.story_TODO.md
    └── archive/
        └── README.md
```

Adjust spec areas to the product. Drop areas that do not apply (for example `02-auth-and-identity` if auth is out of scope for the first release) — do not keep unused areas just because they appear in this template. The scaffolding stories listed above are the recommended opening sequence; feature stories that implement spec items follow them, ordered to respect the dependency graph declared in spec frontmatter.

## Suggested Owner Values

- `frontend`: React UI, client state, styling, browser-side behavior.
- `backend`: ASP.NET Core controllers, services, EF Core, migrations, background jobs.
- `qa`: test plans, Playwright e2e, manual QA, regression gates.
- `devops`: CI, Docker, deployment, configuration, infrastructure.
- `product`: user flows, acceptance criteria, cross-feature semantics.
