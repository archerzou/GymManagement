# Team Git Workflow — Standard Reference

**Branch model:** `main` (production) ← `development` (integration) ← `feature/{ticket-number}-description`

Every team member follows this document for daily work. All commands assume the shared remote is called `origin`.

---

## 1. Branch Naming & Rules

| Branch | Purpose | Who pushes directly |
|---|---|---|
| `main` | Production / release | Nobody (release PRs only) |
| `development` | Integration branch, always buildable | Nobody (PRs only) |
| `feature/{ticket}-{desc}` | One ticket = one branch | The ticket owner |

Naming examples:

```
feature/1234-add-login-validation
feature/1250-fix-fleet-report-export
bugfix/1301-null-ref-on-startup      (optional variant for bugs)
```

**Golden rules**

1. Never commit directly to `development` or `main`.
2. One branch per ticket. Keep branches small and short-lived (1–3 days ideally).
3. Sync your feature branch with `development` **at least once a day**.
4. Resolve conflicts **on your feature branch**, never on `development`.
5. `development` must always compile and pass tests.

---

## 2. One-Time Setup (new team member)

```bash
git clone https://github.com/your-org/your-repo.git
cd your-repo

# Identify yourself
git config user.name  "Your Name"
git config user.email "you@company.com"

# Recommended quality-of-life settings
git config pull.rebase true          # 'git pull' rebases instead of merge-commits
git config fetch.prune true          # auto-remove deleted remote branches
git config rebase.autoStash true     # auto-stash uncommitted work when rebasing

# Make sure you're tracking development
git checkout development
git pull origin development
```

---

## 3. Standard Ticket Lifecycle (the happy path)

### Step 1 — Start from a fresh `development`

**Always** update `development` before branching. Starting from a stale base is the #1 cause of conflicts later.

```bash
git checkout development
git pull origin development          # get the latest integration code
```

### Step 2 — Create your feature branch

```bash
git checkout -b feature/1234-add-login-validation
```

### Step 3 — Work and commit in small units

```bash
# ...edit code...
git add src/Auth/LoginValidator.cs
git commit -m "feat(1234): add email format validation"

# ...more work...
git add .
git commit -m "feat(1234): add unit tests for login validator"
```

Commit message convention (recommended):

```
<type>(<ticket>): <short description>

feat(1234): add login validation
fix(1301): handle null fleet ID on startup
refactor(1250): extract report builder service
test(1234): cover edge cases for validator
```

### Step 4 — Push your branch to the remote (early and often)

```bash
# First push: create the remote branch and set upstream
git push -u origin feature/1234-add-login-validation

# Subsequent pushes
git push
```

Pushing early gives you a backup and lets teammates/CI see your work.

### Step 5 — Sync with `development` daily (see Section 4)

### Step 6 — Open a Pull Request

1. Push your latest code.
2. On GitHub / Azure DevOps / GitLab: create a PR **from** `feature/1234-...` **into** `development`.
3. Title: `[1234] Add login validation`. Link the ticket.
4. Ensure CI passes and the PR shows **"no conflicts"** — if it shows conflicts, go to Section 5 first.
5. Get at least one review approval.
6. Merge using the team's chosen strategy (usually **"Squash and merge"** — one clean commit per ticket on `development`).

### Step 7 — Clean up after merge

```bash
git checkout development
git pull origin development                     # now contains your merged work

git branch -d feature/1234-add-login-validation      # delete local branch
git push origin --delete feature/1234-add-login-validation   # delete remote branch
# (most platforms can auto-delete the remote branch on merge — enable it)
```

You are now back at Step 1, ready for the next ticket.

---

## 4. Keeping Your Feature Branch Up To Date

While you work, other members merge their PRs, so `development` moves ahead of you. Your branch becomes **behind** `development`. Sync daily and before opening a PR.

There are two strategies — **the team must pick ONE and use it consistently.**

### Option A — Rebase (recommended: linear, clean history)

```bash
# 1. Update your local copy of development
git checkout development
git pull origin development

# 2. Replay your feature commits on top of the new development
git checkout feature/1234-add-login-validation
git rebase development
```

If conflicts occur during rebase:

```bash
# Git stops at the conflicting commit. Fix the files, then:
git add <fixed-files>
git rebase --continue        # repeat until rebase finishes

# If things go wrong and you want to start over:
git rebase --abort
```

After a rebase, your branch history has been rewritten, so pushing requires force:

```bash
git push --force-with-lease
```

> ⚠️ **`--force-with-lease`, never `--force`.** It refuses to overwrite work someone else pushed to your branch.
> ⚠️ **Never rebase a branch that others are actively committing to.** Rebase is safe only on your own feature branch.

### Option B — Merge (simpler, safe for shared branches)

```bash
git checkout development
git pull origin development

git checkout feature/1234-add-login-validation
git merge development
```

If conflicts occur:

```bash
# Fix conflicted files, then:
git add <fixed-files>
git commit                   # completes the merge commit
git push                     # normal push, no force needed
```

**Comparison**

| | Rebase | Merge |
|---|---|---|
| History | Linear, clean | Merge commits accumulate |
| Push | Needs `--force-with-lease` | Normal push |
| Conflict resolution | Per-commit (may repeat) | Once, all at the same time |
| Safe when branch is shared | ❌ No | ✅ Yes |
| Recommended for | Solo feature branches | Shared / long-lived branches |

---

## 5. Resolving Conflicts — Step by Step

Conflicts happen when you and `development` changed the **same lines** of the same file. You will see:

```
CONFLICT (content): Merge conflict in src/Services/FleetService.cs
```

### The resolution procedure

```bash
# 1. See which files conflict
git status

# 2. Open each conflicted file. You'll find markers:
#    <<<<<<< HEAD
#    your version
#    =======
#    development's version
#    >>>>>>> development
#
#    Edit the file to the correct final code and DELETE all markers.

# 3. Mark each file as resolved
git add src/Services/FleetService.cs

# 4. Continue
git rebase --continue        # if you were rebasing
git commit                   # if you were merging

# 5. Build and run tests BEFORE pushing — resolved code must actually work
dotnet build && dotnet test

# 6. Push
git push --force-with-lease  # after rebase
git push                     # after merge
```

Useful conflict helpers:

```bash
git diff                             # inspect conflicts in detail
git checkout --ours   <file>         # take YOUR side entirely
git checkout --theirs <file>         # take the OTHER side entirely
git mergetool                        # open a visual merge tool (Rider/VS have great ones)
```

> ⚠️ `--ours` / `--theirs` are **swapped during rebase** (because rebase replays your commits *onto* development, "ours" = development). When unsure, open the file and resolve by hand or use your IDE's merge tool.

### Where to resolve conflicts

**Always on your feature branch.** The flow is: pull latest `development` into your branch (rebase or merge) → resolve → test → push → the PR becomes conflict-free and can be merged cleanly. Nobody ever resolves conflicts directly on `development`.

---

## 6. Common Situations & What To Do

### Situation A — "My branch is behind `development`" (development moved ahead)

The normal daily case. Do Section 4 (rebase or merge from `development`). Do this **before opening a PR** and **whenever a teammate's PR merges that touches your area**.

### Situation B — "My PR shows merge conflicts"

Someone merged first and touched the same files.

```bash
git checkout development && git pull origin development
git checkout feature/1234-...
git rebase development           # or: git merge development
# resolve conflicts (Section 5), test
git push --force-with-lease      # or: git push (merge strategy)
```

The PR will refresh and show "able to merge".

### Situation C — Two members must merge around the same time

Whoever merges **second** inherits the conflicts. Procedure:

1. Member 1's PR is approved → merges to `development`.
2. Member 2 immediately syncs: pull `development` into their feature branch, resolves conflicts locally, tests, pushes.
3. Member 2's PR is now clean → merges.

Never race to merge a conflicted PR; always sync first.

### Situation D — "My feature is ahead / long-running (development can't wait for it)"

For multi-week features:

- Rebase/merge from `development` **daily** — many small syncs beat one giant conflict at the end.
- Break the ticket into smaller PRs where possible (e.g., merge the data layer first, then the API layer).
- Consider a **feature flag**: merge incomplete-but-inert code into `development` early so the diff never grows huge.

### Situation E — "I committed to `development` by accident"

```bash
# On development, with 1 accidental local commit not yet pushed:
git branch feature/1234-rescued        # save your work to a branch
git reset --hard origin/development    # restore development to remote state
git checkout feature/1234-rescued      # continue there
```

If you already **pushed** to development: tell the team immediately; revert with a PR:

```bash
git revert <bad-commit-sha>
git push origin development    # or via a quick PR, per team policy
```

### Situation F — "I started my branch from a stale development"

You branched days ago without pulling first. Fix is the same as a normal sync:

```bash
git checkout development && git pull origin development
git checkout feature/1234-... && git rebase development
```

### Situation G — "I need a teammate's unmerged code"

Avoid if possible (wait for their PR). If truly required:

```bash
git fetch origin
git checkout feature/1234-mine
git merge origin/feature/1250-theirs     # merge, do NOT rebase a shared base
```

Note your PR will then contain their commits until their PR merges.

### Situation H — "I have uncommitted work but must switch branches / pull"

```bash
git stash push -m "wip: 1234 validator"   # shelve changes
# ...switch branch, pull, whatever you need...
git stash pop                             # restore changes
git stash list                            # see all stashes
```

### Situation I — "I need to undo my last commit"

```bash
git reset --soft HEAD~1     # undo commit, keep changes staged
git reset --hard HEAD~1     # undo commit AND discard changes (careful!)
git commit --amend          # just fix the last commit's message/content (before pushing)
```

Never rewrite commits that are already in `development`.

---

## 7. How the Team Avoids Conflicts (prevention > cure)

1. **Small tickets, small PRs.** A 200-line PR merges in hours; a 2,000-line PR rots for a week and conflicts with everything.
2. **Sync daily.** Every member pulls `development` into their branch every morning (Section 4).
3. **Merge PRs quickly.** Review within one working day. Stale approved PRs are conflict factories.
4. **Communicate on shared files.** If two tickets touch `FleetService.cs`, agree on ordering in stand-up, or sequence the tickets.
5. **Don't reformat unrelated code.** Mass auto-format in a feature PR conflicts with everyone. Keep formatting-only changes in their own PR. Use a shared `.editorconfig` so formatting is consistent by default.
6. **Squash-merge to development.** One commit per ticket keeps history readable and makes reverts trivial.
7. **Delete merged branches.** Enable auto-delete on merge.
8. **Protect the branches.** Set `development` and `main` to require: PR, 1+ approval, passing CI, up-to-date branch before merge.

---

## 8. Quick Reference Cheat Sheet

```bash
# ── Start a ticket ─────────────────────────────────────────────
git checkout development
git pull origin development
git checkout -b feature/1234-short-description

# ── Daily work ─────────────────────────────────────────────────
git add <files>
git commit -m "feat(1234): message"
git push -u origin feature/1234-short-description   # first push
git push                                            # after that

# ── Daily sync with development (rebase style) ─────────────────
git checkout development && git pull origin development
git checkout feature/1234-short-description
git rebase development
#   conflicts? fix files → git add <file> → git rebase --continue
git push --force-with-lease

# ── Daily sync (merge style) ───────────────────────────────────
git checkout development && git pull origin development
git checkout feature/1234-short-description
git merge development
#   conflicts? fix files → git add <file> → git commit
git push

# ── Open PR ────────────────────────────────────────────────────
#   feature/1234-...  →  development   (via web UI)
#   CI green + 1 approval + no conflicts → Squash and merge

# ── After merge ────────────────────────────────────────────────
git checkout development && git pull origin development
git branch -d feature/1234-short-description
git push origin --delete feature/1234-short-description

# ── Inspect ────────────────────────────────────────────────────
git status                        # what's changed / conflicted
git log --oneline --graph --all   # visualize branch history
git fetch origin                  # refresh remote info without merging
git diff development...HEAD       # what your branch adds vs development
```

---

## 9. Visual Overview

```
main        ──●───────────────────────────────●──────────►  (releases)
               \                             /
development ────●────●────●────●────●────●──●────────────►  (integration)
                 \        \      \   ↑    ↑
                  \        \      \  PR   PR (squash merge)
feature/1234 ──────●──●──●─┼──────●──┘    │
                            \   (rebase/merge from development)
feature/1250 ────────────────●──●──●──●───┘
```

Flow per ticket: **pull development → branch → commit → sync daily → push → PR → review + CI → squash merge → delete branch → repeat.**
