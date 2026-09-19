import './App.css'

function App() {
  return (
    <main className="dashboard">
      <nav className="topbar"><span className="wordmark"><span className="dot" /> fluxo</span><span className="nav-caption">Visão geral</span><button className="avatar">DO</button></nav>
      <section className="hero"><div><span className="eyebrow">CONTA OPERACIONAL</span><h1>Seu dinheiro,<br /><em>em movimento.</em></h1><p>Acompanhe entradas, saídas e o saldo do seu negócio em um só lugar.</p><button className="primary">+ Novo lançamento</button></div><div className="balance-card"><span>Saldo disponível</span><strong>R$ 24.580,40</strong><small>Atualizado agora · <b>current</b></small><div className="sparkline">╱╲╱╲╱╲╱╲╱╲╱</div></div></section>
      <section className="summary-grid"><article><span>Entradas no mês</span><strong className="positive">R$ 18.240,00</strong><small>↑ 12,4% vs. mês anterior</small></article><article><span>Saídas no mês</span><strong>R$ 6.890,20</strong><small>↓ 4,8% vs. mês anterior</small></article><article><span>Transações</span><strong>48</strong><small>5 aguardando conciliação</small></article></section>
      <section className="activity"><div className="section-heading"><div><span className="eyebrow">ATIVIDADE RECENTE</span><h2>Últimos lançamentos</h2></div><button className="secondary">Ver todos →</button></div><div className="transaction"><span className="transaction-icon in">↙</span><div><strong>Recebimento — Loja Centro</strong><small>Hoje, 09:42 · Receita</small></div><b className="positive">+ R$ 2.400,00</b></div><div className="transaction"><span className="transaction-icon out">↗</span><div><strong>Fornecedor Silva & Filhos</strong><small>Ontem, 16:20 · Operacional</small></div><b>- R$ 890,00</b></div></section>
    </main>
  )
}

export default App
