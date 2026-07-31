import React, { useState, useEffect } from 'react';
import axios from 'axios';

// Pointing to your .NET API port
const API_URL = 'http://localhost:5272/api/transactions';

export default function App() {
  const [transactions, setTransactions] = useState([]);
  const [form, setForm] = useState({ accountHolder: '', amount: '', transactionType: 'Deposit' });
  const [editingId, setEditingId] = useState(null);

  // Fetch all transactions from C# API (READ)
  const fetchTransactions = async () => {
    try {
      const response = await axios.get(API_URL);
      setTransactions(response.data);
    } catch (error) {
      console.error('Error fetching data:', error);
    }
  };

  useEffect(() => {
    fetchTransactions();
  }, []);

  // Handle Create or Update submission (CREATE / UPDATE)
  const handleSubmit = async (e) => {
    e.preventDefault();
    const payload = {
      ...form,
      amount: parseFloat(form.amount)
    };

    try {
      if (editingId) {
        // UPDATE (PUT)
        await axios.put(`${API_URL}/${editingId}`, { ...payload, id: editingId });
        setEditingId(null);
      } else {
        // CREATE (POST)
        await axios.post(API_URL, payload);
      }
      setForm({ accountHolder: '', amount: '', transactionType: 'Deposit' });
      fetchTransactions();
    } catch (error) {
      console.error('Error saving transaction:', error);
    }
  };

  // Populate form for Editing
  const handleEdit = (tx) => {
    setEditingId(tx.id);
    setForm({
      accountHolder: tx.accountHolder,
      amount: tx.amount,
      transactionType: tx.transactionType
    });
  };

  // Delete transaction (DELETE)
  const handleDelete = async (id) => {
    try {
      await axios.delete(`${API_URL}/${id}`);
      fetchTransactions();
    } catch (error) {
      console.error('Error deleting transaction:', error);
    }
  };

  return (
    <div style={{ padding: '30px', fontFamily: 'Arial, sans-serif', maxWidth: '800px', margin: '0 auto' }}>
      <h2>Fintech Kenya Ltd - Mock Transaction Portal</h2>

      {/* CREATE / EDIT FORM */}
      <form onSubmit={handleSubmit} style={{ display: 'flex', gap: '10px', marginBottom: '20px' }}>
        <input 
          type="text"
          placeholder="Account Holder Name" 
          value={form.accountHolder} 
          onChange={e => setForm({...form, accountHolder: e.target.value})} 
          required 
          style={{ padding: '8px', flex: '2' }}
        />
        <input 
          type="number" 
          step="0.01"
          placeholder="Amount (KES)" 
          value={form.amount} 
          onChange={e => setForm({...form, amount: e.target.value})} 
          required 
          style={{ padding: '8px', flex: '1' }}
        />
        <select 
          value={form.transactionType} 
          onChange={e => setForm({...form, transactionType: e.target.value})}
          style={{ padding: '8px' }}
        >
          <option value="Deposit">Deposit</option>
          <option value="Withdrawal">Withdrawal</option>
        </select>
        <button type="submit" style={{ padding: '8px 16px', cursor: 'pointer' }}>
          {editingId ? 'Update' : 'Add'}
        </button>
      </form>

      {/* READ TABLE */}
      <table border="1" cellPadding="10" cellSpacing="0" style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr style={{ backgroundColor: '#f2f2f2' }}>
            <th>ID</th>
            <th>Account Holder</th>
            <th>Amount</th>
            <th>Type</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {transactions.length === 0 ? (
            <tr>
              <td colSpan="5" style={{ textAlign: 'center' }}>No transactions recorded yet.</td>
            </tr>
          ) : (
            transactions.map((tx) => (
              <tr key={tx.id}>
                <td>{tx.id}</td>
                <td>{tx.accountHolder}</td>
                <td>KES {tx.amount.toFixed(2)}</td>
                <td>{tx.transactionType}</td>
                <td>
                  <button onClick={() => handleEdit(tx)} style={{ marginRight: '5px' }}>Edit</button>
                  <button onClick={() => handleDelete(tx.id)}>Delete</button>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  );
}