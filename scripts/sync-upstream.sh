#!/usr/bin/env bash
#
# sync-upstream.sh — Trae los cambios del WindBot original a tu fork.
#
# Ciclo del día a día:
#   1. fetch upstream   -> descarga la historia del original (NO toca tu código)
#   2. merge            -> integra esos cambios a tu rama actual
#   3. (vos) resolvés conflictos si aparecen
#   4. push origin      -> sube el resultado a tu repo
#
# Usa merge (no rebase) a propósito: conserva el historial y no te obliga
# a push --force. Es el camino seguro.
#
# Uso:
#   ./scripts/sync-upstream.sh            # sincroniza la rama actual
#   ./scripts/sync-upstream.sh --push     # además pushea a origin al terminar

set -euo pipefail

UPSTREAM_REMOTE="upstream"
UPSTREAM_BRANCH="master"
DO_PUSH=false

[[ "${1:-}" == "--push" ]] && DO_PUSH=true

# --- 0. Salvaguarda: el working tree debe estar limpio ---------------------
if [[ -n "$(git status --porcelain)" ]]; then
  echo "✋ Tenés cambios sin commitear. Commiteá o stasheá antes de sincronizar."
  echo "   git status   para ver qué hay pendiente."
  exit 1
fi

if ! git remote get-url "$UPSTREAM_REMOTE" >/dev/null 2>&1; then
  echo "✋ No existe el remote '$UPSTREAM_REMOTE'."
  echo "   git remote add upstream https://code.moenext.com/nanahira/windbot.git"
  exit 1
fi

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"

# --- 1. Traer cambios del original (seguro: no modifica tu código) ---------
echo "→ git fetch $UPSTREAM_REMOTE"
git fetch "$UPSTREAM_REMOTE"

# --- ¿Hay algo nuevo? ------------------------------------------------------
BEHIND="$(git rev-list --count "HEAD..$UPSTREAM_REMOTE/$UPSTREAM_BRANCH")"
if [[ "$BEHIND" -eq 0 ]]; then
  echo "✓ Ya estás al día con $UPSTREAM_REMOTE/$UPSTREAM_BRANCH. Nada que traer."
  exit 0
fi
echo "→ Hay $BEHIND commit(s) nuevos en $UPSTREAM_REMOTE/$UPSTREAM_BRANCH."

# --- 2. Integrar con merge -------------------------------------------------
echo "→ git merge $UPSTREAM_REMOTE/$UPSTREAM_BRANCH"
if git merge "$UPSTREAM_REMOTE/$UPSTREAM_BRANCH"; then
  echo "✓ Merge limpio, sin conflictos."
else
  echo ""
  echo "⚠ Hay CONFLICTOS. Esto es normal, no es un error."
  echo "  1. Abrí los archivos marcados (git status los lista)."
  echo "  2. Decidí qué versión queda en cada conflicto."
  echo "  3. git add <archivo> y luego: git commit"
  echo "  4. Volvé a correr este script con --push, o pusheá a mano."
  exit 1
fi

# --- 3. Push opcional ------------------------------------------------------
if $DO_PUSH; then
  echo "→ git push origin $CURRENT_BRANCH"
  git push origin "$CURRENT_BRANCH"
  echo "✓ Subido a origin/$CURRENT_BRANCH."
else
  echo ""
  echo "✓ Listo en local. Para subirlo:  git push origin $CURRENT_BRANCH"
  echo "  (o corré este script con --push la próxima)"
fi
