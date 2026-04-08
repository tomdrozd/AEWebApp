import React, { useState } from 'react';
import { QueryClient, QueryClientProvider, useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Container,
  Button,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TablePagination,
  Box,
  Typography,
  CircularProgress,
  Alert,
  TextField,
  Grid,
  AppBar,
  Toolbar,
  Chip,
  Autocomplete,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  IconButton,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  Menu,
  MenuItem
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/Delete';
import BookmarkIcon from '@mui/icons-material/Bookmark';
import BookmarkBorderIcon from '@mui/icons-material/BookmarkBorder';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import { activityApi, Activity, ActivityFilter } from './services/api';
import ActivityDetailPanel from './components/ActivityDetailPanel';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: true,
      retry: 1,
      staleTime: 30_000,
    },
  },
});

function ActivityExplorer() {
  const qc = useQueryClient();
  const [syncStatus, setSyncStatus] = useState('');
  const [error, setError] = useState('');

  const [filter, setFilter] = useState<ActivityFilter>({
    pageNumber: 1,
    pageSize: 50,
    startDate: undefined,
    endDate: undefined,
    userSearch: '',
    workloads: undefined,
    operations: undefined,
    resultStatus: undefined
  });

  const [selectedActivity, setSelectedActivity] = useState<Activity | null>(null);
  const [detailPanelOpen, setDetailPanelOpen] = useState(false);

  // Save filter dialog
  const [saveDialogOpen, setSaveDialogOpen] = useState(false);
  const [filterName, setFilterName] = useState('');

  // Saved filters menu
  const [filtersAnchor, setFiltersAnchor] = useState<null | HTMLElement>(null);

  // --- Queries ---
  const activitiesQuery = useQuery({
    queryKey: ['activities', filter],
    queryFn: () => activityApi.getActivities(filter),
  });

  const filterOptionsQuery = useQuery({
    queryKey: ['filter-options'],
    queryFn: () => activityApi.getFilterOptions(),
    staleTime: 5 * 60_000, // 5 min — these change rarely
  });

  const savedFiltersQuery = useQuery({
    queryKey: ['saved-filters'],
    queryFn: () => activityApi.getSavedFilters(),
  });

  const filterOptions = filterOptionsQuery.data ?? { workloads: [], operations: [], statuses: [] };
  const savedFilters = savedFiltersQuery.data ?? [];

  // --- Mutations ---
  const syncMutation = useMutation({
    mutationFn: () => activityApi.syncActivities(),
    onMutate: () => {
      setSyncStatus('Syncing... Connecting to Exchange Online...');
      setError('');
    },
    onSuccess: (result) => {
      setSyncStatus(result.message || 'Sync completed successfully');
      qc.invalidateQueries({ queryKey: ['activities'] });
      qc.invalidateQueries({ queryKey: ['filter-options'] });
    },
    onError: (error: any) => {
      const errorData = error.response?.data;
      let errorMessage = '';
      if (errorData?.error) errorMessage = `Error: ${errorData.error}\n`;
      if (errorData?.details) errorMessage += `Details: ${errorData.details}\n`;
      if (errorData?.hint) errorMessage += `\nHint: ${errorData.hint}`;
      if (!errorMessage) errorMessage = error.message || 'Sync failed. Check console for details.';
      setSyncStatus('');
      setError(errorMessage);
      console.error('Sync failed:', error.response?.data || error);
    },
  });

  const statusMutation = useMutation({
    mutationFn: () => activityApi.getAuthStatus(),
    onMutate: () => { setError(''); },
    onSuccess: (status) => {
      console.log('Auth status:', status);
      let msg = '=== AUTHENTICATION STATUS ===\n\n';

      if (!status.isPwshAvailable) {
        msg += '❌ PowerShell (pwsh): NOT FOUND\n';
        msg += '   Install from: https://github.com/PowerShell/PowerShell/releases\n';
      } else {
        msg += `✅ PowerShell (pwsh): ${status.pwshVersion}\n`;
      }
      msg += '\n';

      if (!status.isModuleInstalled) {
        msg += '❌ PowerShell Module: NOT INSTALLED\n';
        msg += `   Install command: ${status.moduleInstallCommand}\n`;
      } else {
        msg += `✅ PowerShell Module: Installed (v${status.moduleVersion})\n`;
        if (status.modulePath) msg += `   Path: ${status.modulePath}\n`;
      }
      msg += '\n';

      if (!status.isCertificateFound) {
        msg += '❌ Certificate: NOT FOUND\n';
        if (status.certificateThumbprint) msg += `   Looking for: ${status.certificateThumbprint}\n`;
      } else {
        msg += `✅ Certificate: Found\n`;
        msg += `   Thumbprint: ${status.certificateThumbprint}\n`;
        msg += `   Expires: ${new Date(status.certificateExpiry).toLocaleDateString()}\n`;
        if (status.isCertificateExpired) msg += '   ⚠️ WARNING: Certificate is EXPIRED!\n';
      }
      msg += '\n';

      if (!status.isConfigurationValid) {
        msg += '❌ Configuration: INCOMPLETE\n';
        status.configurationErrors?.forEach((err: string) => { msg += `   - ${err}\n`; });
      } else {
        msg += '✅ Configuration: Valid\n';
      }
      msg += '\n';

      msg += '=== CONNECTION TEST ===\n';
      if (status.canConnect === true) {
        msg += '✅ Connection: SUCCESSFUL\n';
        msg += `   ${status.connectionTestResult || 'Connected to Exchange Online'}\n`;
      } else if (status.canConnect === false) {
        msg += '❌ Connection: FAILED\n';
        if (status.connectionTestResult) msg += `   Result: ${status.connectionTestResult}\n`;
        if (status.lastConnectionError) msg += `   Error: ${status.lastConnectionError}\n`;
      } else {
        msg += '⚠️ Connection: NOT TESTED\n';
        msg += '   (Prerequisites not met or test was skipped)\n';
      }
      msg += '\n';

      if (status.powerShellVersion) msg += `PowerShell Version: ${status.powerShellVersion}\n`;
      if (!status.isModuleInstalled && status.searchedPaths?.length > 0) {
        msg += '\nModule searched in:\n';
        status.searchedPaths.slice(0, 5).forEach((p: string) => { msg += `   ${p}\n`; });
      }
      if (status.recommendations?.length > 0) {
        msg += '\n=== RECOMMENDATIONS ===\n';
        status.recommendations.forEach((rec: string) => { msg += `• ${rec}\n`; });
      }
      setSyncStatus(msg);
    },
    onError: () => { setError('Failed to check authentication status'); },
  });

  const saveFilterMutation = useMutation({
    mutationFn: (name: string) => activityApi.saveFilter(name, filter),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['saved-filters'] });
      setSaveDialogOpen(false);
      setFilterName('');
    },
  });

  const deleteFilterMutation = useMutation({
    mutationFn: (id: number) => activityApi.deleteFilter(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['saved-filters'] });
    },
  });

  const loadSavedFilter = (filterJson: string) => {
    try {
      const parsed = JSON.parse(filterJson);
      setFilter({
        pageNumber: 1,
        pageSize: filter.pageSize,
        startDate: parsed.startDate ? new Date(parsed.startDate) : undefined,
        endDate: parsed.endDate ? new Date(parsed.endDate) : undefined,
        userSearch: parsed.userSearch || '',
        workloads: parsed.workloads || undefined,
        operations: parsed.operations || undefined,
        resultStatus: parsed.resultStatus || undefined,
      });
      setFiltersAnchor(null);
    } catch (e) {
      console.error('Failed to parse saved filter:', e);
    }
  };

  const clearFilters = () => {
    setFilter({
      ...filter,
      pageNumber: 1,
      startDate: undefined,
      endDate: undefined,
      userSearch: '',
      workloads: undefined,
      operations: undefined,
      resultStatus: undefined,
    });
  };

  const hasActiveFilters = !!(filter.startDate || filter.endDate || filter.userSearch ||
    filter.workloads?.length || filter.operations?.length || filter.resultStatus);

  const handlePageChange = (_event: unknown, newPage: number) => {
    setFilter({ ...filter, pageNumber: newPage + 1 });
  };

  const handleRowsPerPageChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    setFilter({ ...filter, pageSize: parseInt(event.target.value, 10), pageNumber: 1 });
  };

  const formatDate = (date: Date | string) => {
    if (!date) return '-';
    return new Date(date).toLocaleString();
  };

  const loading = activitiesQuery.isLoading || syncMutation.isPending || statusMutation.isPending;
  const activities = activitiesQuery.data ?? { items: [], totalCount: 0, pageNumber: 1, pageSize: 50, totalPages: 0 };

  return (
    <LocalizationProvider dateAdapter={AdapterDateFns}>
      <Box sx={{ flexGrow: 1 }}>
        <AppBar position="static">
          <Toolbar>
            <Typography variant="h6" component="div" sx={{ flexGrow: 1 }}>
              Activity Explorer - Purview Activity Viewer
            </Typography>
          </Toolbar>
        </AppBar>

        <Container maxWidth="xl">
          <Box sx={{ my: 4 }}>
            {/* Filter Controls */}
            <Paper sx={{ p: 2, mb: 2 }}>
              <Grid container spacing={2} sx={{ alignItems: 'center' }}>
                <Grid size={{ xs: 12, md: 3 }}>
                  <DatePicker
                    label="Start Date"
                    value={filter.startDate || null}
                    onChange={(newValue) => setFilter({ ...filter, pageNumber: 1, startDate: newValue || undefined })}
                    slotProps={{ textField: { fullWidth: true, size: 'small' } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <DatePicker
                    label="End Date"
                    value={filter.endDate || null}
                    onChange={(newValue) => setFilter({ ...filter, pageNumber: 1, endDate: newValue || undefined })}
                    slotProps={{ textField: { fullWidth: true, size: 'small' } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <TextField
                    fullWidth
                    size="small"
                    label="Search User"
                    value={filter.userSearch || ''}
                    onChange={(e) => setFilter({ ...filter, pageNumber: 1, userSearch: e.target.value })}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <Autocomplete
                    multiple
                    size="small"
                    options={filterOptions.workloads}
                    value={filter.workloads || []}
                    onChange={(_, newValue) => setFilter({ ...filter, pageNumber: 1, workloads: newValue.length ? newValue : undefined })}
                    renderInput={(params) => <TextField {...params} label="Workload" />}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <Autocomplete
                    multiple
                    size="small"
                    options={filterOptions.operations}
                    value={filter.operations || []}
                    onChange={(_, newValue) => setFilter({ ...filter, pageNumber: 1, operations: newValue.length ? newValue : undefined })}
                    renderInput={(params) => <TextField {...params} label="Operation" />}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <Autocomplete
                    size="small"
                    options={filterOptions.statuses}
                    value={filter.resultStatus || null}
                    onChange={(_, newValue) => setFilter({ ...filter, pageNumber: 1, resultStatus: newValue || undefined })}
                    renderInput={(params) => <TextField {...params} label="Status" />}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <Box sx={{ display: 'flex', gap: 1 }}>
                    <Button variant="outlined" onClick={clearFilters} disabled={!hasActiveFilters} fullWidth>
                      Clear Filters
                    </Button>
                    <IconButton
                      color="primary"
                      onClick={() => setSaveDialogOpen(true)}
                      disabled={!hasActiveFilters}
                      title="Save current filter"
                    >
                      <BookmarkBorderIcon />
                    </IconButton>
                    <IconButton
                      color="primary"
                      onClick={(e) => setFiltersAnchor(e.currentTarget)}
                      disabled={savedFilters.length === 0}
                      title="Load saved filter"
                    >
                      <BookmarkIcon />
                    </IconButton>
                  </Box>
                </Grid>
              </Grid>
            </Paper>

            {/* Saved Filters Menu */}
            <Menu
              anchorEl={filtersAnchor}
              open={Boolean(filtersAnchor)}
              onClose={() => setFiltersAnchor(null)}
            >
              {savedFilters.map((sf) => (
                <MenuItem key={sf.id} sx={{ display: 'flex', justifyContent: 'space-between', gap: 2 }}>
                  <Typography onClick={() => loadSavedFilter(sf.filterJson)} sx={{ flexGrow: 1, cursor: 'pointer' }}>
                    {sf.name}
                  </Typography>
                  <IconButton
                    size="small"
                    onClick={(e) => { e.stopPropagation(); deleteFilterMutation.mutate(sf.id); }}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </MenuItem>
              ))}
            </Menu>

            {/* Action Buttons */}
            <Box sx={{ mb: 2 }}>
              <Button
                variant="contained"
                onClick={() => syncMutation.mutate()}
                disabled={loading}
                sx={{ mr: 2 }}
              >
                Sync Activities from Purview
              </Button>
              <Button
                variant="outlined"
                onClick={() => activityApi.exportCsv(filter)}
                disabled={loading || activities.items.length === 0}
                sx={{ mr: 2 }}
              >
                Export to CSV
              </Button>
              <Button
                variant="outlined"
                onClick={() => qc.invalidateQueries({ queryKey: ['activities'] })}
                disabled={loading}
                sx={{ mr: 2 }}
              >
                Refresh
              </Button>
              <Button
                variant="outlined"
                onClick={() => statusMutation.mutate()}
                disabled={loading}
                color="info"
              >
                Check Auth Status
              </Button>
            </Box>

            {/* Status Messages */}
            {syncStatus && (
              <Alert severity="info" sx={{ mb: 2, whiteSpace: 'pre-line' }}>
                {syncStatus}
              </Alert>
            )}

            {(error || activitiesQuery.isError) && (
              <Alert severity="error" sx={{ mb: 2, whiteSpace: 'pre-line' }}>
                {error || 'Failed to load activities. Please check if the backend is running.'}
              </Alert>
            )}

            {/* Statistics Summary */}
            <Box sx={{ mb: 2 }}>
              <Chip label={`Total Activities: ${activities.totalCount}`} sx={{ mr: 1 }} />
              <Chip label={`Page ${activities.pageNumber} of ${activities.totalPages || 1}`} />
            </Box>

            {/* Activities Table */}
            {activitiesQuery.isLoading ? (
              <Box sx={{ display: 'flex', justifyContent: 'center', p: 4 }}>
                <CircularProgress />
              </Box>
            ) : (
              <TableContainer component={Paper}>
                <Table>
                  <TableHead>
                    <TableRow>
                      <TableCell>Timestamp</TableCell>
                      <TableCell>User</TableCell>
                      <TableCell>Operation</TableCell>
                      <TableCell>Workload</TableCell>
                      <TableCell>Status</TableCell>
                      <TableCell>Client IP</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {activities.items.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={6} align="center">
                          No activities found. Click "Sync Activities from Purview" to fetch data.
                        </TableCell>
                      </TableRow>
                    ) : (
                      activities.items.map((activity) => (
                        <TableRow
                          key={activity.id}
                          hover
                          onClick={() => {
                            setSelectedActivity(activity);
                            setDetailPanelOpen(true);
                          }}
                          sx={{ cursor: 'pointer' }}
                        >
                          <TableCell>{formatDate(activity.timestamp)}</TableCell>
                          <TableCell>{activity.userPrincipalName || activity.userId || '-'}</TableCell>
                          <TableCell>{activity.operation || '-'}</TableCell>
                          <TableCell>{activity.workload || '-'}</TableCell>
                          <TableCell>
                            <Chip
                              label={activity.resultStatus || 'Unknown'}
                              size="small"
                              color={activity.resultStatus === 'Success' ? 'success' : 'default'}
                            />
                          </TableCell>
                          <TableCell>{activity.clientIP || '-'}</TableCell>
                        </TableRow>
                      ))
                    )}
                  </TableBody>
                </Table>
                <TablePagination
                  rowsPerPageOptions={[10, 25, 50, 100]}
                  component="div"
                  count={activities.totalCount}
                  rowsPerPage={filter.pageSize}
                  page={filter.pageNumber - 1}
                  onPageChange={handlePageChange}
                  onRowsPerPageChange={handleRowsPerPageChange}
                />
              </TableContainer>
            )}
          </Box>
        </Container>
      </Box>

      {/* Activity Detail Panel */}
      <ActivityDetailPanel
        activity={selectedActivity}
        open={detailPanelOpen}
        onClose={() => setDetailPanelOpen(false)}
      />

      {/* Save Filter Dialog */}
      <Dialog open={saveDialogOpen} onClose={() => setSaveDialogOpen(false)}>
        <DialogTitle>Save Current Filter</DialogTitle>
        <DialogContent>
          <TextField
            autoFocus
            fullWidth
            label="Filter Name"
            value={filterName}
            onChange={(e) => setFilterName(e.target.value)}
            sx={{ mt: 1 }}
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setSaveDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={() => saveFilterMutation.mutate(filterName)}
            disabled={!filterName.trim() || saveFilterMutation.isPending}
          >
            Save
          </Button>
        </DialogActions>
      </Dialog>
    </LocalizationProvider>
  );
}

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ActivityExplorer />
    </QueryClientProvider>
  );
}

export default App;
