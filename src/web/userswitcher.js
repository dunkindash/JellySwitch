/* userswitcher.js */
(function(){
  const userSearch = document.getElementById('userSearch');
  const userSelect = document.getElementById('userSelect');
  const btnImpersonate = document.getElementById('btnImpersonate');
  const btnAuthorize = document.getElementById('btnAuthorize');
  const qcCode = document.getElementById('qcCode');
  const logEl = document.getElementById('log');

  function log(msg){
    const ts = new Date().toISOString();
    logEl.textContent += `[${ts}] ${msg}\n`;
    logEl.scrollTop = logEl.scrollHeight;
  }

  async function fetchUsers(query){
    const url = `../Plugin/UserSwitcher/Users?search=${encodeURIComponent(query||'')}`;
    const resp = await fetch(url, { credentials: 'same-origin' });
    if(!resp.ok){ throw new Error(`Failed to load users (${resp.status})`); }
    return await resp.json();
  }

  async function refreshUsers(){
    try{
      const list = await fetchUsers(userSearch.value.trim());
      userSelect.innerHTML = '';
      for(const u of list){
        const opt = document.createElement('option');
        opt.value = u.id || u.Id || '';
        opt.textContent = `${u.name || u.Name}${u.isAdministrator ? ' (Admin)' : ''}${u.isDisabled ? ' [Disabled]' : ''}`;
        userSelect.appendChild(opt);
      }
      log(`Loaded ${list.length} users`);
    }catch(e){
      log(`Error loading users: ${e.message}`);
    }
  }

  async function authorizeCode(){
    const code = (qcCode.value||'').trim().toUpperCase();
    const userId = userSelect.value;
    if(code.length !== 6){
      alert('Code must be 6 characters');
      return;
    }
    if(!userId){ alert('Select a user first'); return; }
    try{
      const resp = await fetch('../Plugin/UserSwitcher/AuthorizeCode',{
        method:'POST',
        headers:{ 'Content-Type':'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify({ code, userId })
      });
      if(!resp.ok){
        const t = await resp.text();
        throw new Error(`Server error ${resp.status}: ${t}`);
      }
      log(`Authorized code ${code} for user ${userId}`);
      alert('Code authorized');
    }catch(e){
      log(`Authorize failed: ${e.message}`);
      alert('Authorization failed');
    }
  }

  async function impersonate(){
    const userId = userSelect.value;
    if(!userId){ alert('Select a user first'); return; }
    try{
      const resp = await fetch('../Plugin/UserSwitcher/Impersonate',{
        method:'POST',
        headers:{ 'Content-Type':'application/json' },
        credentials: 'same-origin',
        body: JSON.stringify({ userId })
      });
      if(!resp.ok){
        const t = await resp.text();
        throw new Error(`Server error ${resp.status}: ${t}`);
      }
      const { impersonationUrl } = await resp.json();
      log(`Opening impersonation tab for ${userId}`);
      window.open(impersonationUrl, '_blank', 'noopener');
    }catch(e){
      log(`Impersonate failed: ${e.message}`);
      alert('Impersonation failed');
    }
  }

  userSearch.addEventListener('input', () => {
    clearTimeout(userSearch._t);
    userSearch._t = setTimeout(refreshUsers, 250);
  });
  btnAuthorize.addEventListener('click', authorizeCode);
  btnImpersonate.addEventListener('click', impersonate);

  refreshUsers();
})();