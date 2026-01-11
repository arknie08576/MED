# MED

Prosty projekt w C# (.NET 9) do testowania klasyfikatorów na danych tabelarycznych (CSV) z oceną metodą **Leave-One-Out**.

Aplikacja:

- wczytuje dane z pliku CSV,
- (opcjonalnie) standaryzuje cechy,
- uruchamia wybrany algorytm klasyfikacji,
- liczy metryki i zapisuje wyniki do plików w katalogu wyjściowym.

## Wymagania

- .NET SDK 9.x

Sprawdzenie:

```powershell
dotnet --version
```

## Uruchamianie

Pomoc (lista opcji):

```powershell
dotnet run -- --help
```

Minimalne uruchomienie (wymagane jest tylko `--data`):

```powershell
dotnet run -- --data .\data\dataset.csv
```

Przykład z wyborem algorytmu, parametru `k`, standaryzacją i katalogiem wyjściowym:

```powershell
dotnet run -- --data .\data\dataset.csv --algo knn --k 5 --standardize --out out
```

## Parametry CLI

- `--data <path.csv>` (wymagane) – ścieżka do pliku CSV.
- `--algo knn|ria|riona` (opcjonalne, domyślnie `knn`) – wybór algorytmu:
  - `knn` – klasyczny kNN (głosowanie większościowe)
  - `riona` – kNN z wagami odległości (głos 1/(d+eps))
  - `ria` – klasyfikator najbliższego centroidu (po jednym centroidzie na klasę)
- `--k <int>` (opcjonalne, domyślnie `3`) – liczba sąsiadów dla `knn`/`riona`.
- `--standardize` (opcjonalne) – standaryzacja cech do średniej 0 i odchylenia 1.
- `--out <dir>` (opcjonalne, domyślnie `out`) – katalog na pliki wynikowe.

## Format danych (CSV)

Loader zakłada:

- pierwsza linia to **nagłówek** (jest pomijana),
- **ostatnia kolumna** to etykieta klasy (tekst),
- pozostałe kolumny to wartości liczbowe cech,
- separator: przecinek `,`.

Przykład:

```csv
f1,f2,f3,label
5.1,3.5,1.4,setosa
4.9,3.0,1.4,setosa
```

## Wyniki

Program zapisuje dwa pliki w katalogu `--out`:

- `*_report_YYYYMMDD_HHMMSS.json` – raport z metrykami (accuracy + metryki per klasa),
- `*_predictions_YYYYMMDD_HHMMSS.csv` – tabela z `y_true` i `y_pred`.

Na konsoli wypisywane jest podsumowanie (accuracy + czas).

## Struktura projektu

- `src/Algorithms/` – klasyfikatory (kNN, centroid, kNN ważony)
- `src/Evaluation/` – Leave-One-Out + metryki
- `src/Output/` – nazewnictwo plików, zapis wyników, pomiar czasu
- `src/` – modele, wczytywanie danych, preprocessing, metryka odległości

## Rozwiązywanie problemów

- Jeśli dostajesz błąd parsowania liczb, sprawdź separator dziesiętny i format pliku (zalecane: kropka `.` jako separator dziesiętny).
- Jeśli CSV nie ma nagłówka albo etykieta nie jest w ostatniej kolumnie, daj znać — dopasuję `DataLoader` i/lub dodam odpowiednie flagi CLI.
