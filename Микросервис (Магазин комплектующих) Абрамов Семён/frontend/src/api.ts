export async function api<T>(path:string,method='GET',body?:unknown):Promise<T>{
 const token=sessionStorage.getItem('shop-token');
 let response:Response;try{response=await fetch((import.meta.env.VITE_API_URL||'')+path,{method,headers:{...(body!==undefined?{'Content-Type':'application/json'}:{}),...(token?{Authorization:'Bearer '+token}:{})},body:body===undefined?undefined:JSON.stringify(body)});}catch{throw Error('Нет связи с магазином. Проверьте запуск API.');}
 if(response.status===204)return undefined as T;
 const data=await response.json().catch(()=>({message:'Сервер вернул неверный ответ.'}));
 if(!response.ok){if(response.status===401){sessionStorage.removeItem('shop-token');window.dispatchEvent(new Event('shop-session-expired'));}throw Error(data.message||'Не удалось выполнить действие.');}
 return data as T;
}

