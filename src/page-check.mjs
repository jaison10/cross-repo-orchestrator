// Checks the page's pure renderers (md, wiki) in index.html between /* pure:begin */ and /* pure:end */.
import { readFileSync } from 'node:fs';
import assert from 'node:assert/strict';

const html = readFileSync(new URL('./OrchestratorGate/page/index.html', import.meta.url), 'utf8');
const block = /\/\* pure:begin \*\/([\s\S]*?)\/\* pure:end \*\//.exec(html);
assert(block, 'pure:begin/end markers missing in index.html');
const { md, wiki } = new Function(block[1] + '\nreturn { md, wiki };')();

const story = `# Copy stories to Jira
Type: Story | Priority: Medium (suggested)

## Summary
As a **PM**, I want \`copy\` so that [Jira](https://x.atlassian.net/browse/A-1) works.

## Scope
- **In:** rendered view
  - nested *sub* point
- **Out:** Server [legacy]

## Dependencies & Order
1. first
   - child
2. second

## Technical Notes
| Field | Value |
|---|---|
| a | \`b\` |

\`\`\`js
const x = {a: 1};
\`\`\``;

const storyWiki = `h1. Copy stories to Jira
Type: Story | Priority: Medium (suggested)

h2. Summary
As a *PM*, I want {{copy}} so that [Jira|https://x.atlassian.net/browse/A-1] works.

h2. Scope
* *In:* rendered view
** nested _sub_ point
* *Out:* Server \\[legacy\\]

h2. Dependencies & Order
# first
#* child
# second

h2. Technical Notes
||Field||Value||
|a|{{b}}|

{code}
const x = {a: 1};
{code}`;

assert.equal(md('- a\n  - b\n- c'), '<ul><li>a<ul><li>b</li></ul></li><li>c</li></ul>');
assert.equal(md('1. one\n   - sub\n2. two'), '<ol><li>one<ul><li>sub</li></ul></li><li>two</li></ol>');
assert.equal(md('1. a\n\n2. b'), '<ol><li>a</li><li>b</li></ol>');
assert.equal(md('3. c'), '<ol start="3"><li>c</li></ol>');
assert.equal(md('- [ ] todo\n- [x] done'), '<ul><li>☐ todo</li><li>☑ done</li></ul>');
assert.equal(md('**b** and `c`'), '<p><strong>b</strong> and <code>c</code></p>');

const unsafe = md('<img src=x onerror=alert(1)> [a](javascript:alert(1))');
assert.doesNotMatch(unsafe, /<img/);
assert.doesNotMatch(unsafe, /<a /);

assert.equal(wiki(story), storyWiki);

const storyHtml = md(story);
assert.ok(storyHtml.includes('<ul><li><strong>In:</strong> rendered view<ul><li>nested <em>sub</em> point</li></ul></li>'), storyHtml);
assert.ok(storyHtml.includes('<table>'));
assert.ok(storyHtml.includes('<pre><code>'));

// Review fixes: unclosed fence, emphasis in link text, piped cells, code span with **, CRLF.
assert.equal(wiki('```\nx'), '{code}\nx\n{code}');
assert.equal(wiki('[**b** _i_](https://x.y)'), '[*b* _i_|https://x.y]');
assert.equal(md('| a \\| b | `c|d` |'), '<table><tbody><tr><td>a | b</td><td><code>c|d</code></td></tr></tbody></table>');
assert.equal(wiki('| a \\| b | `c|d` |'), '|a \\| b|{{c\\|d}}|');
assert.equal(md('`a**b**c`'), '<p><code>a**b**c</code></p>');
assert.equal(wiki('# T\r\n\r\n- a\r\n- b'), 'h1. T\n\n* a\n* b');
assert.equal(md('# T\r\n\r\n- a\r\n- b'), '<h1>T</h1><ul><li>a</li><li>b</li></ul>');

console.log('page-check ok');
