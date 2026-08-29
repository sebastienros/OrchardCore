# Management API reference pages

Read this reference whenever adding or changing an Orchard Core OpenAPI management endpoint or its documentation.

## Workflow

1. Inventory every route in the owning endpoint mapping class.
2. Read request/response models, JSON attributes or naming policy, handler/service branches, OpenAPI metadata, and endpoint tests.
3. Create or update `src/docs/reference/api/<resource>/README.md`.
4. Link the canonical page from the owning module README, `src/docs/reference/api/README.md`, and `mkdocs.yml`.
5. Run `python -m mkdocs build --strict`.

Never guess JSON casing, defaults, required fields, constraints, permissions, or status codes. Identify feature-dependent schema portions as dynamic.

## Required page shape

Use this sequence:

1. Purpose and owning/enabling feature.
2. Authentication and exact permissions.
3. Base route.
4. Endpoint summary table with method, path, description, and permission.
5. One section per operation:
   - exact method and route;
   - path, query, header, and body parameters with type, requirement, accepted values, default, and constraints;
   - exact request example or an explicit statement that no body is accepted;
   - every meaningful response status and representative response example.
6. Paging envelope where applicable.
7. Shared errors and behavioral caveats.
8. Endpoint source files used to verify coverage.

Keep the endpoint contract normative in the API page. Module pages should describe the feature and link to the API page instead of duplicating route details.

Use the detailed contributor checklist in `src/docs/contributing/documenting-management-apis.md`.
