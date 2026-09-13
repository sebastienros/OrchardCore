# Query-based content deployment adapter

This branch starts independently at merged campaign commit `e92d895b6`, with no
PR #31 dependency. It adds the query-based content factory's explicit contract.

## Existing callers and shared behavior

`QueryBasedContentDeploymentStepDriver.UpdateAsync` previously looked up the query,
checked `ReturnContentItems`, and parsed optional JSON parameters. It now calls
`QueryDeploymentConfiguration.ValidateAsync`, also used by the API adapter. Missing
or blank query names now produce the same model-state validation error as a query
that does not return content, instead of dereferencing a missing query.

`QueryBasedContentDeploymentSource.ProcessAsync` now uses the shared parser. Its
existing null policy remains: a null property or the JSON string `null` becomes an
empty parameter dictionary. The admin/API configuration rejects the JSON string
`null`, matching the existing editor. Invalid JSON still skips source execution.
Other source execution, content serialization and setup identity rewriting are
unchanged. Admin binding still assigns submitted values after validation for
redisplay; API patches reject before assignment and retain omitted properties.

Tests exercise actual admin update methods, the actual export source's execution
call, and the feature-owned factory/adapter registrations. The strict test-project build passes with zero warnings/errors, all 15 focused
tests pass, and strict documentation builds successfully. Live transport/export
verification and the independent PR gates remain pending.

The live `deployment-query-smoke.py` check now passes on a disposable tenant:
feature discovery, Pomi configuration, MCP parameter clearing, invalid-patch
preservation, and queued export through the existing SQL query source. The
privately downloaded ZIP contains the expected published content item and title.
