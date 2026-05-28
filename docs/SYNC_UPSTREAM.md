# Sync with the original WindBot (upstream)

This fork is a **standalone** repository (`origin = diangogav/windbot`), not a
GitHub fork. The original project lives in `upstream` and we use it only to
**pull its changes** whenever we want. We never push to `upstream` (its push URL
is disabled on purpose).

## Remotes

| Remote     | URL                                              | Use                     |
|------------|--------------------------------------------------|-------------------------|
| `origin`   | `git@github.com:diangogav/windbot.git`           | Your repo (read/push)   |
| `upstream` | `https://code.moenext.com/nanahira/windbot.git`  | Original (read-only)    |

## Daily cycle

The short way — use the script:

```bash
./scripts/sync-upstream.sh          # pull and merge the original's changes
./scripts/sync-upstream.sh --push   # also push the result to your origin
```

The manual way (what the script does under the hood):

```bash
git fetch upstream                  # 1. download the original (does NOT touch your code)
git merge upstream/master           # 2. integrate the changes into your branch
# 3. if there are conflicts, resolve them and: git add <file> && git commit
git push origin master              # 4. push the result to your repo
```

## Why `merge` and not `rebase`

We use **merge**: it preserves history as it actually happened and never forces a
`push --force`. `rebase` rewrites commits and, on already-pushed code, it's an
elegant way to break things. If you don't know git by heart, stick with `merge`.

## Conflicts

If you touched the same file the original touched (typical: `WindBot.csproj`), git
marks the conflict. **It's not an error** — it's git asking you to decide which
version stays. The more your fork diverges, the more conflicts you'll see. That's
the natural price of having your own version.
