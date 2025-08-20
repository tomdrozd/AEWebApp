import React from 'react';
import {
  Drawer,
  Box,
  Typography,
  IconButton,
  Divider,
  Chip,
  Grid,
  Paper,
  Stack,
  Accordion,
  AccordionSummary,
  AccordionDetails,
  Table,
  TableBody,
  TableCell,
  TableRow,
} from '@mui/material';
import CloseIcon from '@mui/icons-material/Close';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import { Activity } from '../services/api';

interface ActivityDetailPanelProps {
  activity: Activity | null;
  open: boolean;
  onClose: () => void;
}

const ActivityDetailPanel: React.FC<ActivityDetailPanelProps> = ({ activity, open, onClose }) => {
  if (!activity) return null;

  const formatDate = (date: Date | string | undefined) => {
    if (!date) return '-';
    const d = new Date(date);
    return d.toLocaleString();
  };

  const formatFileSize = (bytes: number | undefined) => {
    if (!bytes) return '-';
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    if (bytes === 0) return '0 Bytes';
    const i = Math.floor(Math.log(bytes) / Math.log(1024));
    return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i];
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
  };

  const parseJsonField = (jsonString: string | undefined) => {
    if (!jsonString) return null;
    try {
      return JSON.parse(jsonString);
    } catch {
      return jsonString;
    }
  };

  const renderJsonContent = (jsonString: string | undefined, title: string) => {
    const parsed = parseJsonField(jsonString);
    if (!parsed) return null;
    
    return (
      <Accordion>
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Typography variant="subtitle2">{title}</Typography>
        </AccordionSummary>
        <AccordionDetails>
          <Box sx={{ 
            backgroundColor: '#f5f5f5', 
            p: 1, 
            borderRadius: 1,
            fontSize: '0.875rem',
            fontFamily: 'monospace',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word'
          }}>
            {typeof parsed === 'object' ? JSON.stringify(parsed, null, 2) : parsed}
          </Box>
        </AccordionDetails>
      </Accordion>
    );
  };

  const DetailRow = ({ label, value, copyable = false }: { label: string; value: any; copyable?: boolean }) => (
    <TableRow>
      <TableCell component="th" scope="row" sx={{ fontWeight: 'medium', width: '40%' }}>
        {label}
      </TableCell>
      <TableCell>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          {value || '-'}
          {copyable && value && (
            <IconButton size="small" onClick={() => copyToClipboard(value)}>
              <ContentCopyIcon fontSize="small" />
            </IconButton>
          )}
        </Box>
      </TableCell>
    </TableRow>
  );

  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      PaperProps={{
        sx: { width: { xs: '100%', sm: '600px', md: '700px' } }
      }}
    >
      <Box sx={{ p: 3, height: '100%', overflow: 'auto' }}>
        {/* Header */}
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 3 }}>
          <Box>
            <Typography variant="h5" gutterBottom>
              Activity Details
            </Typography>
            <Stack direction="row" spacing={1}>
              <Chip 
                label={activity.operation || 'Unknown Operation'} 
                color="primary"
                size="small"
              />
              <Chip 
                label={activity.resultStatus || 'Unknown'} 
                color={activity.resultStatus === 'Success' ? 'success' : 'default'}
                size="small"
              />
              {activity.workload && (
                <Chip label={activity.workload} variant="outlined" size="small" />
              )}
            </Stack>
          </Box>
          <IconButton onClick={onClose}>
            <CloseIcon />
          </IconButton>
        </Box>

        <Divider sx={{ mb: 2 }} />

        {/* Core Information */}
        <Paper elevation={0} sx={{ p: 2, mb: 2, backgroundColor: '#f8f9fa' }}>
          <Typography variant="h6" gutterBottom>Core Information</Typography>
          <Table size="small">
            <TableBody>
              <DetailRow label="Record ID" value={activity.recordIdentity} copyable />
              <DetailRow label="Timestamp" value={formatDate(activity.timestamp)} />
              <DetailRow label="User" value={activity.userPrincipalName || activity.userId} copyable />
              <DetailRow label="Operation" value={activity.operation} />
              <DetailRow label="Activity ID" value={activity.activityId} />
              <DetailRow label="Workload" value={activity.workload} />
              <DetailRow label="Data Platform" value={activity.dataPlatform} />
              <DetailRow label="Client IP" value={activity.clientIP} copyable />
            </TableBody>
          </Table>
        </Paper>

        {/* Application & Device Information */}
        <Paper elevation={0} sx={{ p: 2, mb: 2, backgroundColor: '#f8f9fa' }}>
          <Typography variant="h6" gutterBottom>Application & Device</Typography>
          <Table size="small">
            <TableBody>
              <DetailRow label="Application" value={activity.application} />
              <DetailRow label="Platform" value={activity.platform} />
              <DetailRow label="Device Name" value={activity.deviceName} />
              <DetailRow label="Source Location" value={activity.sourceLocationType} />
              <DetailRow label="User Type" value={activity.userType} />
              <DetailRow label="User SKU" value={activity.userSku} />
            </TableBody>
          </Table>
        </Paper>

        {/* File & Content Information */}
        {(activity.filePath || activity.itemName || activity.fileSize || activity.contentType) && (
          <Paper elevation={0} sx={{ p: 2, mb: 2, backgroundColor: '#f8f9fa' }}>
            <Typography variant="h6" gutterBottom>File & Content</Typography>
            <Table size="small">
              <TableBody>
                <DetailRow label="Item Name" value={activity.itemName} />
                <DetailRow label="File Path" value={activity.filePath} />
                <DetailRow label="File Size" value={formatFileSize(activity.fileSize)} />
                <DetailRow label="Content Type" value={activity.contentType} />
                <DetailRow label="Object ID" value={activity.objectId} copyable />
              </TableBody>
            </Table>
          </Paper>
        )}

        {/* Sensitivity & Protection */}
        {(activity.sensitivityLabel || activity.howApplied || activity.labelEventType) && (
          <Paper elevation={0} sx={{ p: 2, mb: 2, backgroundColor: '#fff3e0' }}>
            <Typography variant="h6" gutterBottom>Sensitivity & Protection</Typography>
            <Table size="small">
              <TableBody>
                <DetailRow label="Sensitivity Label" value={activity.sensitivityLabel} copyable />
                <DetailRow label="How Applied" value={activity.howApplied} />
                <DetailRow label="Application Detail" value={activity.howAppliedDetail} />
                <DetailRow label="Label Event Type" value={activity.labelEventType} />
                <DetailRow label="Protection Event" value={activity.protectionEventType} />
              </TableBody>
            </Table>
          </Paper>
        )}

        {/* Complex JSON Fields */}
        <Box sx={{ mb: 2 }}>
          {renderJsonContent(activity.emailInfo, 'Email Information')}
          {renderJsonContent(activity.policyMatchInfo, 'Policy Match Information')}
          {renderJsonContent(activity.sensitiveInfoTypeData, 'Sensitive Information Types')}
          {renderJsonContent(activity.sensitiveInfoTypeBucketsData, 'Sensitive Info Buckets')}
          {renderJsonContent(activity.sensitivityLabelIdsReferenced, 'Referenced Label IDs')}
          {renderJsonContent(activity.attachmentDetails, 'Attachment Details')}
        </Box>

        {/* Metadata */}
        <Paper elevation={0} sx={{ p: 2, backgroundColor: '#f8f9fa' }}>
          <Typography variant="h6" gutterBottom>Metadata</Typography>
          <Table size="small">
            <TableBody>
              <DetailRow label="Activity ID" value={activity.id} copyable />
              <DetailRow label="Created At" value={formatDate(activity.createdAt)} />
              <DetailRow label="Updated At" value={formatDate(activity.updatedAt)} />
            </TableBody>
          </Table>
        </Paper>
      </Box>
    </Drawer>
  );
};

export default ActivityDetailPanel;