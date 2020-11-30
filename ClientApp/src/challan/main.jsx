import React from 'react';
import { createRoot } from 'react-dom/client';
import ChallanSenderApp from './ChallanSenderApp.jsx';

const el = document.getElementById('challan-sender-app');
if (el) {
  createRoot(el).render(
    <ChallanSenderApp
      lookupsUrl={el.dataset.lookupsUrl}
      saveUrl={el.dataset.saveUrl}
      listUrl={el.dataset.listUrl}
    />
  );
}
