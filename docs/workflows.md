# Workflows

Open **Workflows** in the title bar to create an ordered sequence of requests.
Add saved requests or the current tab, and use the arrows to reorder them.
Each step stores a copy of the request at the time it is added. Later edits to
that request's tab do not change the workflow; remove and re-add the step to
use an updated request.

For a register → authenticated API → logout sequence:

1. Send the registration request once in its tab to obtain a JSON response.
2. Open **Workflows**, name the workflow, and add the registration request.
3. Choose **Select JSON fields…**, select `/access_token` (or the nested field
   your API returns), and enter `access_token` as the environment variable name.
4. Set the API and logout requests' **Auth** type to **Bearer**, with
   `{{access_token}}` as the token, then add them to the workflow.
5. **Save** the definition or **Run workflow** to save and execute it.

The picker uses an existing response from the request's open tab or the latest
run of that workflow step. It supports nested objects and array elements.
Paths use JSON Pointer notation: `/data/access_token`, `/users/0/id`; `~1`
represents a slash in a property name and `~0` represents a tilde.

Each successful step captures the selected string, number, or boolean fields
and saves them to the environment before starting the next step. These variables
are also available to ordinary requests and autocomplete. Captures replace existing
variables of the same name and leave unrelated variables intact. Definitions and
captured variables are stored locally with the workspace; collection exports do
not include workflows.

A non-2xx response, request error, missing/null/empty captured value, invalid or
truncated JSON, or save failure stops the workflow. All captures for one step are
validated before any are saved. Cancel stops further requests; completed steps
and their captured variables remain saved. Workflows do not retry requests or
perform automatic logout/rollback after a failure.

Save edits before closing the workflow window. Running also saves the current
workflow. Captured credentials have the same local storage behavior as other
environment variables.
