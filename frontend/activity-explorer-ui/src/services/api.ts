import axios from 'axios';

const API_BASE = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api';

export interface Activity {
  id: string;
  timestamp: Date;
  userId?: string;
  userPrincipalName?: string;
  operation?: string;
  workload?: string;
  resultStatus?: string;
  clientIP?: string;
  objectId?: string;
  targetUser?: string;
  
  // New fields from Export-ActivityExplorerData
  recordIdentity?: string;
  activityId?: string;
  application?: string;
  contentType?: string;
  dataPlatform?: string;
  deviceName?: string;
  filePath?: string;
  itemName?: string;
  fileSize?: number;
  platform?: string;
  sourceLocationType?: string;
  userType?: string;
  userSku?: string;
  
  // Sensitivity and Protection fields
  sensitivityLabel?: string;
  howApplied?: string;
  howAppliedDetail?: string;
  labelEventType?: string;
  protectionEventType?: string;
  
  // Complex fields (JSON strings)
  emailInfo?: string;
  policyMatchInfo?: string;
  sensitiveInfoTypeData?: string;
  sensitiveInfoTypeBucketsData?: string;
  sensitivityLabelIdsReferenced?: string;
  attachmentDetails?: string;
  
  // Metadata
  createdAt?: Date;
  updatedAt?: Date;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface ActivityFilter {
  startDate?: Date;
  endDate?: Date;
  workloads?: string[];
  operations?: string[];
  userSearch?: string;
  resultStatus?: string;
  pageNumber: number;
  pageSize: number;
}

export interface SavedFilter {
  id: number;
  name: string;
  userId: string;
  filterJson: string;
  createdAt: string;
}

export interface FilterOptions {
  workloads: string[];
  operations: string[];
  statuses: string[];
}

export const activityApi = {
  getActivities: async (filter: ActivityFilter): Promise<PagedResult<Activity>> => {
    const response = await axios.get(`${API_BASE}/activities`, {
      params: {
        startDate: filter.startDate?.toISOString(),
        endDate: filter.endDate?.toISOString(),
        workloads: filter.workloads,
        operations: filter.operations,
        userSearch: filter.userSearch,
        resultStatus: filter.resultStatus,
        pageNumber: filter.pageNumber,
        pageSize: filter.pageSize,
      },
      paramsSerializer: { indexes: null }, // serialize arrays as workloads=a&workloads=b
    });
    return response.data;
  },
  
  syncActivities: async () => {
    const response = await axios.post(`${API_BASE}/activities/sync`);
    return response.data;
  },
  
  getAuthStatus: async () => {
    const response = await axios.get(`${API_BASE}/activities/status`);
    return response.data;
  },
  
  exportCsv: async (filter?: ActivityFilter) => {
    const params = filter ? {
      ...filter,
      startDate: filter.startDate?.toISOString(),
      endDate: filter.endDate?.toISOString()
    } : {};
    
    const response = await axios.get(`${API_BASE}/activities/export/csv`, { 
      responseType: 'blob',
      params
    });
    
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `activities_${new Date().toISOString().split('T')[0]}.csv`);
    document.body.appendChild(link);
    link.click();
    link.remove();
  },
  
  getFilterOptions: async (): Promise<FilterOptions> => {
    const response = await axios.get(`${API_BASE}/activities/filter-options`);
    return response.data;
  },

  getSavedFilters: async (): Promise<SavedFilter[]> => {
    const response = await axios.get(`${API_BASE}/filters`);
    return response.data;
  },

  saveFilter: async (name: string, filter: ActivityFilter): Promise<SavedFilter> => {
    const { pageNumber, pageSize, ...filterData } = filter;
    const response = await axios.post(`${API_BASE}/filters`, {
      name,
      userId: 'default',
      filterJson: JSON.stringify(filterData),
    });
    return response.data;
  },

  deleteFilter: async (id: number): Promise<void> => {
    await axios.delete(`${API_BASE}/filters/${id}`);
  },

  getStatistics: async (startDate?: Date, endDate?: Date) => {
    const response = await axios.get(`${API_BASE}/activities/statistics`, {
      params: {
        startDate: startDate?.toISOString(),
        endDate: endDate?.toISOString()
      }
    });
    return response.data;
  }
};