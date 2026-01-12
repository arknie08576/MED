# MED — kNN, RIA i RIONA (Leave-One-Out)

Projekt implementuje klasyfikację **k+NN**, **RIA** oraz **RIONA** dla danych mieszanych
(numeryczne + nominalne) z walidacją **Leave-One-Out (LOO)**.

Obsługiwane są:
- dystans globalny i lokalny,
- nominalne miary **SVDM / SVDM’**,
- dwie strategie obsługi braków danych,
- imputacja warunkowa względem klasy,
- automatyczne generowanie plików `kNN_*`, `OUT_*`, `STAT_*`.

Projekt był testowany na zbiorze **nursery.data**.

---

## Budowanie i uruchamianie

```bash
dotnet build
