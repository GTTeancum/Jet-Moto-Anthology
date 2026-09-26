"""Restore tiny SELECT/START samples; do not invent molded controller print."""
from restore_credits_marks import main


if __name__ == '__main__':
    main('MISC', 'CONTROL',
         {page: [(278, 326, 39, 12), (324, 326, 34, 12)] for page in range(5)},
         'menu-controller-print-candidates')
