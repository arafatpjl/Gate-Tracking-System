import React from 'react';
import { createRoot } from 'react-dom/client';
import BuyerApp from './BuyerApp.jsx';

const el = document.getElementById('buyer-app');
if (el) {
  const apiBase = el.dataset.apiBase || '/Buyer';
  createRoot(el).render(<BuyerApp apiBase={apiBase} />);
}
