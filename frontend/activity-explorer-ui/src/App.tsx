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
  Chip
} from '@mui/material';
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
      staleTime: 30_000, // 30s before data is considered stale
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
    userSearch: ''
  });

  const [selectedActivity, setSelectedActivity] = useState<Activity | null>(null);
  const [detailPanelOpen, setDetailPanelOpen] = useState(false);

  // --- Queries ---
  const activitiesQuery = useQuery({
    queryKey: ['activities', filter.pageNumber, filter.pageSize, filter.startDate?.toISOString(), filter.endDate?.toISOString(), filter.userSearch],
    queryFn: () => activityApi.getActivities(filter),
  });

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
    onMutate: () => {
      setError('');
    },
    onSuccess: (status) => {
      console.log('Auth status:', status);
      let msg = '=== AUTHENTICATION STATUS ===\n\n';

      // pwsh status
      if (!status.isPwshAvailable) {
        msg += '❌ PowerShell (pwsh): NOT FOUND\n';
        msg += '   Install from: https://github.com/PowerShell/PowerShell/releases\n';
      } else {
        msg += `✅ PowerShell (pwsh): ${status.pwshVersion}\n`;
      }
      msg += '\n';

      // Module status
      if (!status.isModuleInstalled) {
        msg += '❌ PowerShell Module: NOT INSTALLED\n';
        msg += `   Install command: ${status.moduleInstallCommand}\n`;
      } else {
        msg += `✅ PowerShell Module: Installed (v${status.moduleVersion})\n`;
        if (status.modulePath) msg += `   Path: ${status.modulePath}\n`;
      }
      msg += '\n';

      // Certificate status
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

      // Configuration status
      if (!status.isConfigurationValid) {
        msg += '❌ Configuration: INCOMPLETE\n';
        status.configurationErrors?.forEach((err: string) => { msg += `   - ${err}\n`; });
      } else {
        msg += '✅ Configuration: Valid\n';
      }
      msg += '\n';

      // Connection status
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
    onError: () => {
      setError('Failed to check authentication status');
    },
  });

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

  const handleActivityClick = (activity: Activity) => {
    setSelectedActivity(activity);
    setDetailPanelOpen(true);
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
                    onChange={(newValue) => setFilter({ ...filter, startDate: newValue || undefined })}
                    slotProps={{ textField: { fullWidth: true } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <DatePicker
                    label="End Date"
                    value={filter.endDate || null}
                    onChange={(newValue) => setFilter({ ...filter, endDate: newValue || undefined })}
                    slotProps={{ textField: { fullWidth: true } }}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <TextField
                    fullWidth
                    label="Search User"
                    value={filter.userSearch || ''}
                    onChange={(e) => setFilter({ ...filter, userSearch: e.target.value })}
                  />
                </Grid>
                <Grid size={{ xs: 12, md: 3 }}>
                  <Button
                    variant="outlined"
                    onClick={() => setFilter({ ...filter, startDate: undefined, endDate: undefined, userSearch: '' })}
                    fullWidth
                  >
                    Clear Filters
                  </Button>
                </Grid>
              </Grid>
            </Paper>

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
                          onClick={() => handleActivityClick(activity)}
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
