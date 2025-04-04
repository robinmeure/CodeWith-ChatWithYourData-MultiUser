import { useMsal } from "@azure/msal-react";
import { useState, useEffect } from "react";
import { env } from "../config/env";

export const useAuth = () => {
    const { instance } = useMsal();
    const [accessToken, setAccessToken] = useState<string>("");
    const userId = instance.getAllAccounts()[0]?.localAccountId || "";
    
    let accessTokenRequest = { 
        scopes: [env.BACKEND_SCOPE],
        account: instance.getAllAccounts()[0]
    };

    useEffect(() => {
        const fetchData = async () => {
            try {
                const response = await instance.acquireTokenSilent(accessTokenRequest);
                setAccessToken(response.accessToken);
            } catch (error) {
                console.error('Silent token acquisition failed. Acquiring token using redirect.', error);
                // Try interactive token acquisition as fallback
                try {
                    const response = await instance.acquireTokenPopup(accessTokenRequest);
                    setAccessToken(response.accessToken);
                } catch (interactiveError) {
                    console.error('Interactive token acquisition failed.', interactiveError);
                }
            }
        };
        
        if (accessTokenRequest.account) {
            fetchData();
        } else {
            console.error('No account found. User may not be authenticated.');
        }
    }, [instance]);

    return { userId, accessToken }; 
};
