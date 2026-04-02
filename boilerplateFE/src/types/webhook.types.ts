export interface WebhookEndpoint {
  id: string;
  url: string;
  description: string | null;
  events: string[];
  isActive: boolean;
  createdAt: string;
  modifiedAt: string | null;
  lastDeliveryStatus: string | null;
  lastDeliveryAt: string | null;
}

export interface WebhookDelivery {
  id: string;
  eventType: string;
  requestPayload: string;
  responseStatusCode: number | null;
  responseBody: string | null;
  status: 'Pending' | 'Success' | 'Failed';
  duration: number | null;
  attemptCount: number;
  errorMessage: string | null;
  createdAt: string;
}

export interface WebhookEventType {
  type: string;
  resource: string;
  description: string;
}

export interface CreateWebhookData {
  url: string;
  description?: string;
  events: string[];
  isActive: boolean;
}

export interface UpdateWebhookData extends CreateWebhookData {
  id: string;
}

export interface CreateWebhookResponse {
  id: string;
  secret: string;
}
