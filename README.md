## MED

Aplikacja konsolowa (.NET) do uruchamiania eksperymentów klasyfikacyjnych dla danych nominalnych/mieszanych.
Obsługiwane algorytmy: **kNN**, **RIA**, **RIONA**.

### Wymagania

- .NET SDK (projekt buduje się jako `net9.0`)

### Dane

- Przykładowy zbiór: `data/nursery.data`
- Plik z nazwami atrybutów: `data/nursery.names`

### Ważne uwagi

- Domyślnie wykonywana jest imputacja braków (klasowo-warunkowa). Aby ją wyłączyć użyj `--no-impute`.
- Jeśli dane **nie mają nagłówka**, użyj `--no-header`.

---

## Gotowe polecenia uruchomieniowe (copy & paste)

Poniższe polecenia zakładają, że:

* zbiór danych znajduje się w `data/nursery.data`,
* dane **nie posiadają nagłówka** (`--no-header`),
* wyniki mają być zapisane do katalogu `out/`.

---

### 1) Pełny eksperyment (ZALECANE)

Jednym poleceniem generuje:

* kNN dla `k = 1`, `k = 3`, `k = log2(n)`,
* RIONA (`OUT_*` + `STAT_*`) dla `k = 3`.

```bash
dotnet run -- --all --data data/nursery.data --no-header --k 3 --mode g --outdir out
```

---

### 2) Pełny eksperyment + RIA (wolne)

Dodatkowo generuje **RIA** (`OUT_*` + `STAT_*`).

```bash
dotnet run -- --all --all-ria --data data/nursery.data --no-header --k 3 --mode g --outdir out
```

⚠️ Uwaga: RIA jest obliczeniowo kosztowne (kilkanaście–kilkadziesiąt minut).

---

### 3) kNN (Leave-One-Out), `k = log2(n)`, dystans lokalny

```bash
dotnet run -- --alg knn --data data/nursery.data --no-header --k log2n --mode l --outdir out
```

Generuje plik:

```
kNN_knn_nursery_klog2n_l_svdm_v1.txt
```

---

### 4) kNN (Leave-One-Out), `k = 3`, dystans globalny

```bash
dotnet run -- --alg knn --data data/nursery.data --no-header --k 3 --mode g --outdir out
```

---

### 5) RIONA (Leave-One-Out), `k = 3`

```bash
dotnet run -- --alg riona --data data/nursery.data --no-header --k 3 --mode g --outdir out
```

Generuje:

* `OUT_riona_*.csv`
* `STAT_riona_*.txt`

---

### 6) RIA (Leave-One-Out)

```bash
dotnet run -- --alg ria --data data/nursery.data --no-header --mode g --outdir out
```

---

### 7) RIONA z innymi ustawieniami (przykład)

Dystans lokalny + SVDM’ + drugi wariant obsługi braków:

```bash
dotnet run -- --alg riona --data data/nursery.data --no-header --k log2n --mode l --nomdist svdmprime --missing v2 --outdir out
```

---

### 8) Uruchomienie bez zapisu plików (tylko wynik w konsoli)

```bash
dotnet run -- --alg knn --data data/nursery.data --no-header --k 3 --mode g
```

---

### 9) Pomoc (lista parametrów)

```bash
dotnet run -- --help
```

---

**Rekomendacja:**
Do sprawdzania projektu wystarczy uruchomić **punkt 9.1** (`--all`), który generuje komplet wymaganych plików wynikowych.
