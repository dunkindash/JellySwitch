/* userswitcher.js */
(function(){
  function setup(page){
    // Functional elements only
    const userSearch = page.querySelector('#userSearch');
    const userSelect = page.querySelector('#userSelect');
    const btnImpersonate = page.querySelector('#btnImpersonate');
    const btnAuthorize = page.querySelector('#btnAuthorize');
    const qcCode = page.querySelector('#qcCode');
    const logEl = page.querySelector('#log');
    
    // Ensure required elements exist
    if (!userSearch || !userSelect || !btnImpersonate || !btnAuthorize || !qcCode || !logEl) {
      console.log('User Switcher: Required elements not found, skipping setup');
      return;
    }

    function log(msg){
      const ts = new Date().toISOString();
      logEl.textContent += `[${ts}] ${msg}\n`;
      logEl.scrollTop = logEl.scrollHeight;
    }

    function api(path){
      const base = `${window.location.origin}${(window.AppInfo && AppInfo.baseUrl) || ''}`;
      return `${base}/Plugin/UserSwitcher/${path}`;
    }


    async function fetchUsers(query){
      const url = api(`Users?search=${encodeURIComponent(query||'')}`);
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
        const resp = await fetch(api('AuthorizeCode'),{
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
        const resp = await fetch(api('Impersonate'),{
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

    // Event listeners for functional tools
    userSearch.addEventListener('input', () => {
      clearTimeout(userSearch._t);
      userSearch._t = setTimeout(refreshUsers, 250);
    });
    btnAuthorize.addEventListener('click', authorizeCode);
    btnImpersonate.addEventListener('click', impersonate);

    // Initialize functional tools
    refreshUsers();
  }

  document.addEventListener('viewshow', function(e){
    const page = e.target;
    
    // Handle configuration page (settings only)
    if(page && page.classList && page.classList.contains('userswitcherConfigurationPage')){
      setupConfigurationPage(page);
    }
    
    // Handle tools page (functional interface)
    if(page && page.classList && page.classList.contains('userswitcherToolsPage')){
      setupToolsPage(page);
    }
  });

  // Configuration page setup (settings only)
  function setupConfigurationPage(page) {
    const impersonationMinutes = page.querySelector('#impersonationMinutes');
    const watermarkImpersonation = page.querySelector('#watermarkImpersonation');
    const btnSaveConfig = page.querySelector('#btnSaveConfig');
    const configStatus = page.querySelector('#configStatus');
    
    if (!impersonationMinutes || !btnSaveConfig) {
      // Elements not found, probably wrong page
      return;
    }

    function showConfigStatus(msg, isError = false) {
      if (configStatus) {
        configStatus.textContent = msg;
        configStatus.style.color = isError ? '#d32f2f' : '#2e7d32';
        setTimeout(() => {
          configStatus.textContent = '';
        }, 3000);
      }
    }

    async function loadConfiguration() {
      try {
        const resp = await fetch(`${window.location.origin}/System/Configuration/UserSwitcher`, {
          credentials: 'same-origin'
        });
        
        if (!resp.ok) {
          throw new Error(`Failed to load configuration (${resp.status})`);
        }
        
        const config = await resp.json();
        
        // Set form values
        impersonationMinutes.value = config.ImpersonationMinutes || 15;
        if (watermarkImpersonation) {
          watermarkImpersonation.checked = config.WatermarkImpersonation !== false;
        }
        
        showConfigStatus('Configuration loaded successfully');
      } catch (e) {
        console.error('Error loading configuration:', e);
        // Set default values if loading fails
        impersonationMinutes.value = 15;
        if (watermarkImpersonation) {
          watermarkImpersonation.checked = true;
        }
        showConfigStatus('Loaded default configuration', true);
      }
    }

    async function saveConfiguration() {
      try {
        const config = {
          ImpersonationMinutes: parseInt(impersonationMinutes.value) || 15,
          WatermarkImpersonation: watermarkImpersonation ? watermarkImpersonation.checked : true
        };
        
        // Validate input
        if (config.ImpersonationMinutes < 1 || config.ImpersonationMinutes > 1440) {
          throw new Error('Impersonation minutes must be between 1 and 1440');
        }
        
        const resp = await fetch(`${window.location.origin}/System/Configuration/UserSwitcher`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          credentials: 'same-origin',
          body: JSON.stringify(config)
        });
        
        if (!resp.ok) {
          const errorText = await resp.text();
          throw new Error(`Failed to save configuration (${resp.status}): ${errorText}`);
        }
        
        showConfigStatus('Configuration saved successfully');
      } catch (e) {
        console.error('Error saving configuration:', e);
        showConfigStatus(`Error: ${e.message}`, true);
      }
    }

    // Configuration event listeners
    btnSaveConfig.addEventListener('click', saveConfiguration);
    
    // Initialize
    loadConfiguration();
  }

  // Tools page setup (functional interface)
  function setupToolsPage(page) {
    setup(page);
  }
})();