/*
Defining the main namespace
*/

angular
.module('talon.common', [
    'ui.router',
    'datatables',
    'datatables.bootstrap',
    'talon.dataAccess',
    'dialogs.main',
    'toaster',
    'ngStorage',
    'ui.grid',
    'ui.grid.pagination'
]);