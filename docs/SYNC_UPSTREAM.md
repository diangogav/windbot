# Sincronizar con el WindBot original (upstream)

Este fork es un repositorio **independiente** (`origin = diangogav/windbot`), no un
fork de GitHub. El proyecto original vive en `upstream` y lo usamos solo para
**traer sus cambios** cuando queramos. No pusheamos nunca a `upstream` (su push
está deshabilitado a propósito).

## Remotes

| Remote     | URL                                              | Uso                    |
|------------|--------------------------------------------------|------------------------|
| `origin`   | `git@github.com:diangogav/windbot.git`           | Tu repo (lectura/push) |
| `upstream` | `https://code.moenext.com/nanahira/windbot.git`  | Original (solo lectura)|

## Ciclo del día a día

La forma corta — usá el script:

```bash
./scripts/sync-upstream.sh          # trae y mergea los cambios del original
./scripts/sync-upstream.sh --push   # además sube el resultado a tu origin
```

La forma manual (lo que el script hace por dentro):

```bash
git fetch upstream                  # 1. descarga el original (NO toca tu código)
git merge upstream/master           # 2. integra los cambios a tu rama
# 3. si hay conflictos, resolvelos y: git add <archivo> && git commit
git push origin master              # 4. subí el resultado a tu repo
```

## Por qué `merge` y no `rebase`

Usamos **merge**: conserva el historial tal como pasó y no obliga a `push --force`.
`rebase` reescribe commits y, sobre código ya pusheado, es una forma elegante de
romper cosas. Si no dominás git de memoria, quedate con `merge`.

## Conflictos

Si tocaste el mismo archivo que tocó el original (típico: `WindBot.csproj`), git
marca el conflicto. **No es un error** — es git pidiéndote que decidas vos qué
versión queda. Cuanto más diverja tu fork, más conflictos vas a ver. Es el precio
natural de tener tu propia versión.
