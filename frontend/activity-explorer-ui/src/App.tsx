import React, { useState, useEffect } from 'react';
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
import { activityApi, Activity, ActivityFilter, PagedResult } from './services/api';
import ActivityDetailPanel from './components/ActivityDetailPanel';

function App() {
  const [activities, setActivities] = useState<PagedResult<Activity>>({
    items: [],
    totalCount: 0,
    pageNumber: 1,
    pageSize: 50,
    totalPages: 0
  });
  const [loading, setLoading] = useState(false);
  const [syncStatus, setSyncStatus] = useState('');
  const [error, setError] = useState('');
  const [statusInfo, setStatusInfo] = useState<any>(null);
  const [showStatus, setShowStatus] = useState(false);
  
  const [filter, setFilter] = useState<ActivityFilter>({
    pageNumber: 1,
    pageSize: 50,
    startDate: undefined,
    endDate: undefined,
    userSearch: ''
  });
  
  const [selectedActivity, setSelectedActivity] = useState<Activity | null>(null);
  const [detailPanelOpen, setDetailPanelOpen] = useState(false);

  const loadActivities = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await activityApi.getActivities(filter);
      setActivities(data);
    } catch (error: any) {
      console.error('Failed to load activities:', error);
      setError('Failed to load activities. Please check if the backend is running.');
    }
    setLoading(false);
  };

  const handleSync = async () => {
    setLoading(true);
    setSyncStatus('Syncing... Connecting to Exchange Online...');
    setError('');
    try {
      const result = await activityApi.syncActivities();
      setSyncStatus(result.message || 'Sync completed successfully');
      await loadActivities();
    } catch (error: any) {
      const errorData = error.response?.data;
      let errorMessage = '';
      
      // Build detailed error message
      if (errorData?.error) {
        errorMessage = `Error: ${errorData.error}\n`;
      }
      
      if (errorData?.details) {
        errorMessage += `Details: ${errorData.details}\n`;
      }
      
      if (errorData?.hint) {
        errorMessage += `\nHint: ${errorData.hint}`;
      }
      
      // If no structured error data, use the raw message
      if (!errorMessage) {
        errorMessage = error.message || 'Sync failed. Check console for details.';
      }
      
      setSyncStatus('');
      setError(errorMessage);
      console.error('Sync failed - Full error:', error.response?.data || error);
    }
    setLoading(false);
  };

  const handleExport = async () => {
    try {
      await activityApi.exportCsv(filter);
    } catch (error) {
      console.error('Export failed:', error);
      setError('Export failed');
    }
  };

  const handleCheckStatus = async () => {
    setLoading(true);
    setError('');
    try {
      const status = await activityApi.getAuthStatus();
      setStatusInfo(status);
      setShowStatus(true);
      console.log('Auth status:', status);
      
      // Build status message
      let statusMessage = '=== AUTHENTICATION STATUS ===\n\n';
      
      // Module status
      if (!status.isModuleInstalled) {
        statusMessage += '❌ PowerShell Module: NOT INSTALLED\n';
        statusMessage += `   Install command: ${status.moduleInstallCommand}\n`;
      } else {
        statusMessage += `✅ PowerShell Module: Installed (v${status.moduleVersion})\n`;
        if (status.modulePath) {
          statusMessage += `   Path: ${status.modulePath}\n`;
        }
      }
      statusMessage += '\n';
      
      // Certificate status
      if (!status.isCertificateFound) {
        statusMessage += '❌ Certificate: NOT FOUND\n';
        if (status.certificateThumbprint) {
          statusMessage += `   Looking for: ${status.certificateThumbprint}\n`;
        }
      } else {
        statusMessage += `✅ Certificate: Found\n`;
        statusMessage += `   Thumbprint: ${status.certificateThumbprint}\n`;
        statusMessage += `   Expires: ${new Date(status.certificateExpiry).toLocaleDateString()}\n`;
        if (status.isCertificateExpired) {
          statusMessage += '   ⚠️ WARNING: Certificate is EXPIRED!\n';
        }
      }
      statusMessage += '\n';
      
      // Configuration status
      if (!status.isConfigurationValid) {
        statusMessage += '❌ Configuration: INCOMPLETE\n';
        if (status.configurationErrors && status.configurationErrors.length > 0) {
          status.configurationErrors.forEach((error: string) => {
            statusMessage += `   - ${error}\n`;
          });
        }
      } else {
        statusMessage += '✅ Configuration: Valid\n';
      }
      statusMessage += '\n';
      
      // Connection status
      statusMessage += '=== CONNECTION TEST ===\n';
      if (status.canConnect === true) {
        statusMessage += '✅ Connection: SUCCESSFUL\n';
        statusMessage += `   ${status.connectionTestResult || 'Connected to Exchange Online'}\n`;
      } else if (status.canConnect === false) {
        statusMessage += '❌ Connection: FAILED\n';
        if (status.connectionTestResult) {
          statusMessage += `   Result: ${status.connectionTestResult}\n`;
        }
        if (status.lastConnectionError) {
          statusMessage += `   Error: ${status.lastConnectionError}\n`;
        }
      } else {
        statusMessage += '⚠️ Connection: NOT TESTED\n';
        statusMessage += '   (Prerequisites not met or test was skipped)\n';
      }
      statusMessage += '\n';
      
      // PowerShell info
      if (status.powerShellVersion) {
        statusMessage += `PowerShell Version: ${status.powerShellVersion}\n`;
      }
      
      // Searched paths (if module not found)
      if (!status.isModuleInstalled && status.searchedPaths && status.searchedPaths.length > 0) {
        statusMessage += '\nModule searched in:\n';
        status.searchedPaths.slice(0, 5).forEach((path: string) => {
          statusMessage += `   ${path}\n`;
        });
      }
      
      // Recommendations
      if (status.recommendations && status.recommendations.length > 0) {
        statusMessage += '\n=== RECOMMENDATIONS ===\n';
        status.recommendations.forEach((rec: string) => {
          statusMessage += `• ${rec}\n`;
        });
      }
      
      setSyncStatus(statusMessage);
    } catch (error: any) {
      setError('Failed to check authentication status');
      console.error('Status check failed:', error);
    }
    setLoading(false);
  };

  const handlePageChange = (event: unknown, newPage: number) => {
    setFilter({ ...filter, pageNumber: newPage + 1 });
  };

  const handleRowsPerPageChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    setFilter({ ...filter, pageSize: parseInt(event.target.value, 10), pageNumber: 1 });
  };

  const formatDate = (date: Date | string) => {
    if (!date) return '-';
    const d = new Date(date);
    return d.toLocaleString();
  };
  
  const handleActivityClick = (activity: Activity) => {
    setSelectedActivity(activity);
    setDetailPanelOpen(true);
  };
  
  const handleCloseDetailPanel = () => {
    setDetailPanelOpen(false);
  };

  useEffect(() => {
    loadActivities();
  }, [filter.pageNumber, filter.pageSize, filter.startDate, filter.endDate]);

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
              <Grid container spacing={2} alignItems="center">
                <Grid item xs={12} md={3}>
                  <DatePicker
                    label="Start Date"
                    value={filter.startDate || null}
                    onChange={(newValue) => setFilter({ ...filter, startDate: newValue || undefined })}
                    slotProps={{ textField: { fullWidth: true } }}
                  />
                </Grid>
                <Grid item xs={12} md={3}>
                  <DatePicker
                    label="End Date"
                    value={filter.endDate || null}
                    onChange={(newValue) => setFilter({ ...filter, endDate: newValue || undefined })}
                    slotProps={{ textField: { fullWidth: true } }}
                  />
                </Grid>
                <Grid item xs={12} md={3}>
                  <TextField
                    fullWidth
                    label="Search User"
                    value={filter.userSearch || ''}
                    onChange={(e) => setFilter({ ...filter, userSearch: e.target.value })}
                  />
                </Grid>
                <Grid item xs={12} md={3}>
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
                onClick={handleSync} 
                disabled={loading}
                sx={{ mr: 2 }}
              >
                Sync Activities from Purview
              </Button>
              <Button 
                variant="outlined" 
                onClick={handleExport}
                disabled={loading || activities.items.length === 0}
                sx={{ mr: 2 }}
              >
                Export to CSV
              </Button>
              <Button 
                variant="outlined" 
                onClick={loadActivities}
                disabled={loading}
                sx={{ mr: 2 }}
              >
                Refresh
              </Button>
              <Button 
                variant="outlined" 
                onClick={handleCheckStatus}
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
            
            {error && (
              <Alert severity="error" sx={{ mb: 2, whiteSpace: 'pre-line' }}>
                {error}
              </Alert>
            )}

            {/* Statistics Summary */}
            <Box sx={{ mb: 2 }}>
              <Chip label={`Total Activities: ${activities.totalCount}`} sx={{ mr: 1 }} />
              <Chip label={`Page ${activities.pageNumber} of ${activities.totalPages || 1}`} />
            </Box>

            {/* Activities Table */}
            {loading ? (
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
        onClose={handleCloseDetailPanel}
      />
    </LocalizationProvider>
  );
}

export default App;