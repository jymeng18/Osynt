# Workflows

Open **Workflows** in the title bar. The left side shows your workflows and run
order; selecting a step opens its request and response inspector on the right.
Names, steps, order, included steps, request edits, and variable links save
automatically. **All changes saved** confirms a successful write. Edits also
flush when switching workflows or closing the window. Failed saves retain your
edits and offer **Retry save**.

Add a whole folder from the request picker, add a single saved request, or use
**Add current tab**. Use the arrows to change order and uncheck requests to skip
them. Each step keeps its own request copy; edit its URL, body, and authentication
inside the inspector. Changes to a request's original tab do not alter that copy.

For a register → authenticated API → logout sequence:

1. Add the registration, API, and logout requests, or add their folder together.
2. Select registration and click **Send step**. There is no need to leave the
   workflow editor or run the whole sequence to get a response.
3. Select `access_token` in **Response → variables**. Its variable name is suggested
   automatically; change it if needed, then click **Set as variable**.
4. The value and its response-field link save together. It is immediately available
   as `{{access_token}}`, and subsequent runs refresh it from that response field.
5. Click **Use as Bearer in next step**, or select the API request and choose
   **Bearer** authentication. Type `{{` or press **Ctrl+Space** to insert a variable.
   Set up the logout request the same way.
6. Click **Run workflow**. Each successful step captures its selected values before
   the next request begins. Results appear beside each step.

The response inspector supports searching nested fields and array entries.
Paths use JSON Pointer notation: `/data/access_token`, `/users/0/id`; `~1`
represents a slash in a property name and `~0` represents a tilde. Only nonempty
strings, numbers, and booleans are available for capture.

Variable suggestions show their source (Environment or an earlier included step)
and a value preview or a pending-value hint. Credential-like names have masked
previews. Suggestions support substring matching, arrow keys, Enter/Tab to insert,
and Escape to dismiss. Completion works in the request editor and workflow
inspector, including multiline bodies, and replaces existing references without
adding duplicate braces. Environment variable edits also save automatically.

Captures replace existing variables of the same name and preserve unrelated
variables. Definitions and captured values are stored locally with the workspace;
collection exports do not include workflows. A non-2xx response, request error,
missing/null/empty capture, incomplete/invalid JSON, or save failure stops a run.
All captures for one step are validated before any are saved. **Stop** cancels
further requests; completed steps and captured variables remain saved. Runs do not
retry requests or automatically log out after a failure.

The interaction design draws on Postman's [response-to-variable flow and automatic
variable saving](https://learning.postman.com/latest-v-12/docs/use/send-requests/variables/define-variables)
and [Collection Runner](https://learning.postman.com/docs/tests-and-scripts/running-collections/intro-to-collection-runs).
