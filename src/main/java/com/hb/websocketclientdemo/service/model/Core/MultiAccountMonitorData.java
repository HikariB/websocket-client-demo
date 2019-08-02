package com.hb.websocketclientdemo.service.model.Core;

import com.hb.websocketclientdemo.service.model.LoginResult;
import com.hb.websocketclientdemo.service.model.MonitorData;
import com.hb.websocketclientdemo.service.model.SubResult;
import org.springframework.stereotype.Component;

import java.util.HashMap;
import java.util.LinkedList;
import java.util.List;
import java.util.Map;

@Component
public class MultiAccountMonitorData {
    private Map<String, MonitorData> accountsInfo;

    private List<LoginResult> loginResult;

    private SubResult subResult;

//    private Map<String>


    public MultiAccountMonitorData() {
        this.accountsInfo = new HashMap<>();
        this.loginResult = new LinkedList<>();
        this.subResult = new SubResult();
    }


    public List<LoginResult> getLoginResult() {
        return loginResult;
    }

    public void setLoginResult(List<LoginResult> loginResult) {
        this.loginResult = loginResult;
    }

    public SubResult getSubResult() {
        return subResult;
    }

    public void setSubResult(SubResult subResult) {
        this.subResult = subResult;
    }


    public Map<String, MonitorData> getAccountsInfo() {
        return accountsInfo;
    }

    public void setAccountsInfo(Map<String, MonitorData> accountsInfo) {
        this.accountsInfo = accountsInfo;
    }
}
