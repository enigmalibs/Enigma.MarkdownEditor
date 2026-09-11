# Tables

A table is a grid of bordered cells with an emphasised header row, and the separator row decides
each column's alignment.

| left | centre | right |
|:-----|:------:|------:|
| one  | two    | three |
| a    | b      | c     |

Cells keep their inline formatting, and a row with fewer cells than the header leaves the rest
empty rather than shifting everything left.

| what     | how          | note                     |
|----------|--------------|--------------------------|
| **bold** | `code span`  | [a link](./other-note.md) |
| *italic* | ~~struck~~   |                          |

A table wider than the pane scrolls sideways on its own rather than losing its last column.

| id | title                                   | tags                        | created    | author            |
|---:|:----------------------------------------|:----------------------------|:-----------|:------------------|
|  1 | A note with a deliberately long title    | preview, fixture, wide      | 2026-08-28 | someone@example.com |
|  2 | Another one, so the row count is not one | table, alignment, scrolling | 2026-08-28 | someone@example.com |

Text after the tables.
